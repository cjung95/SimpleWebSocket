// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket.Contracts;
using Jung.SimpleWebSocket.Exceptions;
using Jung.SimpleWebSocket.Models;
using Jung.SimpleWebSocket.Wrappers;
using Microsoft.Extensions.Logging;
using System.Net.WebSockets;
using System.Text;

namespace Jung.SimpleWebSocket
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleWebSocketServer"/> class that listens
    /// for incoming connection attempts on the specified local IP address and port number.
    /// </summary>
    /// <param name="hostName">The host name of the Server</param>
    /// <param name="port">A port on which to listen for incoming connection attempts</param>
    /// <param name="requestPath">The web socket request path</param>
    /// <param name="logger">A logger to write internal log messages</param>
    public class SimpleWebSocketClient(string hostName, int port, string requestPath, ILogger? logger = null) : SimpleWebSocketBase(logger), IWebSocketClient, IDisposable
    {
        /// <inheritdoc/>
        public string HostName { get; } = hostName;
        /// <inheritdoc/>
        public int Port { get; } = port;
        /// <inheritdoc/>
        public string RequestPath { get; } = requestPath;

        /// <inheritdoc/>
        public bool IsConnected => _client?.Connected ?? false;

        /// <inheritdoc/>
        public event Action? Disconnected;
        /// <inheritdoc/>
        public event Action<string>? MessageReceived;
        /// <inheritdoc/>
        public event Action<byte[]>? BinaryMessageReceived;

        /// <summary>
        /// The CancellationTokenSource for the client.
        /// </summary>
        private CancellationTokenSource _cancellationTokenSource = new();

        /// <summary>
        /// The client that is used to connect to the server.
        /// </summary>
        private TcpClientWrapper? _client;

        /// <summary>
        /// The WebSocket that is used to communicate with the server.
        /// </summary>
        private IWebSocket? _webSocket;

        /// <summary>
        /// The stream that is used to communicate with the server.
        /// </summary>
        private INetworkStream? _stream;

        /// <inheritdoc/>
        public async Task ConnectAsync(CancellationToken? cancellationToken = null)
        {
            if (IsConnected) throw new WebSocketServerException(message: "Client is already connected");
            cancellationToken ??= CancellationToken.None;

            _cancellationTokenSource = new CancellationTokenSource();
            var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken.Value, _cancellationTokenSource.Token);

            try
            {
                _client = new TcpClientWrapper();
                await _client.ConnectAsync(HostName, Port);
                await HandleWebSocketInitiation(_client, linkedTokenSource.Token);

                LogInternal("Connection upgraded, now listening.");
                _ = ProcessWebSocketMessagesAsync(_webSocket!, linkedTokenSource.Token);
            }
            catch (Exception exception)
            {
                _logger?.LogError(exception, "Error accepting _client");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task DisconnectAsync(string closingStatusDescription = "Closing", CancellationToken? cancellationToken = null)
        {
            cancellationToken ??= CancellationToken.None;
            if (_webSocket != null && (_webSocket.State == WebSocketState.Open || _webSocket.State == WebSocketState.CloseReceived))
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, closingStatusDescription, cancellationToken.Value);
            }
            _client?.Dispose();
        }

        /// <summary>
        /// Handles the WebSocket initiation.
        /// </summary>
        /// <param name="client">The client to use for the WebSocket initiation</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The asynchronous task</returns>
        private async Task HandleWebSocketInitiation(TcpClientWrapper client, CancellationToken cancellationToken)
        {
            // Upgrade the connection to a WebSocket
            _stream = client.GetStream();
            var socketWrapper = new SocketWrapper(_stream);

            var requestContext = WebContext.CreateRequest(HostName, Port, RequestPath);
            await socketWrapper.SendUpgradeRequestAsync(requestContext, cancellationToken);
            var response = await socketWrapper.AwaitContextAsync(cancellationToken);
            SocketWrapper.ValidateUpgradeResponse(response, requestContext);
            _webSocket = socketWrapper.CreateWebSocket(isServer: false);
        }

        /// <inheritdoc/>
        public async Task SendMessageAsync(string message, CancellationToken? cancellationToken = null)
        {
            if (!IsConnected) throw new WebSocketServerException(message: "Client is not connected");
            if (_webSocket == null) throw new WebSocketServerException(message: "WebSocket is not initialized");
            cancellationToken ??= CancellationToken.None;

            try
            {
                // Send the message
                var buffer = Encoding.UTF8.GetBytes(message);
                await _webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, cancellationToken.Value);
                LogInternal("Message sent", $"Message sent: \"{message}\"");
            }
            catch (Exception exception)
            {
                throw new WebSocketServerException(message: "Error sending message", innerException: exception);
            }
        }

        /// <summary>
        /// Processes the WebSocket messages.
        /// </summary>
        /// <param name="webSocket">The WebSocket whose messages to process</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>An asynchronous task</returns>
        /// <exception cref="InvalidOperationException"></exception>
        private async Task ProcessWebSocketMessagesAsync(IWebSocket webSocket, CancellationToken cancellationToken)
        {
            if (webSocket == null)
            {
                throw new InvalidOperationException("WebSocket is not initialized");
            }

            var buffer = new byte[1024 * 4]; // Buffer for incoming data
            while (webSocket.State == WebSocketState.Open)
            {

                // Read the next message
                WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    // Handle the text message
                    string receivedMessage = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    LogInternal("Message received", $"Message received: \"{receivedMessage}\"");
                    _ = Task.Run(() => MessageReceived?.Invoke(receivedMessage), cancellationToken);
                }
                else if (result.MessageType == WebSocketMessageType.Binary)
                {
                    // Handle the binary message
                    LogInternal($"Binary message received", $"Binary message received, length: {result.Count} bytes");
                    _ = Task.Run(() => BinaryMessageReceived?.Invoke(buffer[..result.Count]), cancellationToken);
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    LogInternal("WebSocket closed");
                    _ = Task.Run(() => Disconnected?.Invoke(), cancellationToken);
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    break;
                }
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _stream?.Dispose();
            _client?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}