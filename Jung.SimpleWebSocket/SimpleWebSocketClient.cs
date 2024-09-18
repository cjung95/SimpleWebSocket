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
    public class SimpleWebSocketClient(string hostName, int port, string requestPath, ILogger? logger = null) : IWebSocketClient, IDisposable
    {
        /// <inheritdoc/>
        public event Action<string>? MessageReceived;
        /// <inheritdoc/>
        public event Action<byte[]>? BinaryMessageReceived;
        /// <inheritdoc/>
        public event Action<object?>? ClientDisconnected;
        /// <inheritdoc/>
        public event Action<object?>? ClientConnected;

        /// <inheritdoc/>
        public string HostName { get; } = hostName;
        /// <inheritdoc/>
        public int Port { get; } = port;
        /// <inheritdoc/>
        public string RequestPath { get; } = requestPath;

        private CancellationTokenSource _cancellationTokenSource = new();
        private ITcpClient? _client;
        private IWebSocket? _webSocket;
        private INetworkStream? _stream;
        private readonly ILogger? _logger = logger;

        /// <inheritdoc/>
        public async Task ConnectAsync(CancellationToken cancellation)
        {
            _cancellationTokenSource = new CancellationTokenSource();
            var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _cancellationTokenSource.Token);

            try
            {
                _client = new TcpClientWrapper();
                await _client.ConnectAsync(HostName, Port);
                await HandleWebsocketInitiation(_client, linkedTokenSource.Token);

                LogInternal("Connection upgraded, now listening.");
                _ = HandleConnection(linkedTokenSource.Token);
            }
            catch (Exception exception)
            {
                _logger?.LogError(exception, "Error accepting _client");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task DisconnectAsync()
        {
            if (_webSocket != null && (_webSocket.State == WebSocketState.Open || _webSocket.State == WebSocketState.CloseReceived))
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
            }
            _client?.Dispose();
        }

        private async Task HandleWebsocketInitiation(ITcpClient client, CancellationToken cancellationToken)
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
        public async Task SendMessageAsync(string message, CancellationToken cancellationToken)
        {
            if (_webSocket != null)
            {
                try
                {
                    // Send the message
                    var buffer = Encoding.UTF8.GetBytes(message);
                    await _webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, cancellationToken);
                    LogInternal("Message sent", $"Message sent: \"{message}\"");
                }
                catch (Exception exception)
                {
                    throw new WebSocketServerException(message: "Error sending message", innerException: exception);
                }
            }
        }

        private async Task HandleConnection(CancellationToken cancellationToken)
        {
            if (_webSocket == null)
            {
                throw new InvalidOperationException("WebSocket is not initialized");
            }

            var buffer = new byte[1024 * 4]; // Buffer for incoming data
            while (_webSocket.State == WebSocketState.Open)
            {

                // Read the next message
                WebSocketReceiveResult result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

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
                    LogInternal("WebSocket closed by _client");
                    _ = Task.Run(() => ClientDisconnected?.Invoke(null), cancellationToken);
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
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

        private void LogInternal(string infoLogMessage, string debugLogMessage = "")
        {
#pragma warning disable CA2254 // Template should be a static expression
            if (!string.IsNullOrEmpty(debugLogMessage) && (_logger?.IsEnabled(LogLevel.Debug) ?? false))
            {
                _logger?.LogDebug(debugLogMessage);
            }
            else
            {
                _logger?.LogInformation(infoLogMessage);
            }
#pragma warning restore CA2254 // Template should be a static expression
        }
    }
}

