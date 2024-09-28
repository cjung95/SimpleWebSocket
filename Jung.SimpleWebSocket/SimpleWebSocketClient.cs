// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket.Contracts;
using Jung.SimpleWebSocket.Delegates;
using Jung.SimpleWebSocket.Exceptions;
using Jung.SimpleWebSocket.Models;
using Jung.SimpleWebSocket.Models.EventArguments;
using Jung.SimpleWebSocket.Wrappers;
using Microsoft.Extensions.Logging;
using System.Net.WebSockets;
using System.Text;

namespace Jung.SimpleWebSocket
{
    /// <summary>
    /// A simple WebSocket client.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="SimpleWebSocketClient"/> class that connects to a WebSocket server.
    /// </remarks>
    /// <param name="hostName">The host name to connect to</param>
    /// <param name="port">The port to connect to</param>
    /// <param name="requestPath">The web socket request path</param>
    /// <param name="logger">A logger to write internal log messages</param>
    public class SimpleWebSocketClient(string hostName, int port, string requestPath, string? userId = null, ILogger? logger = null) : IWebSocketClient, IDisposable
    {
        /// <inheritdoc/>
        public string HostName { get; } = hostName;
        /// <inheritdoc/>
        public int Port { get; } = port;
        /// <inheritdoc/>
        public string RequestPath { get; } = requestPath;

        /// <inheritdoc/>
        public string? UserId { get; private set; }

        /// <inheritdoc/>
        public bool IsConnected => _client?.Connected ?? false;

        /// <inheritdoc/>
        public event DisconnectedEventHandler? Disconnected;
        /// <inheritdoc/>
        public event MessageReceivedEventHandler? MessageReceived;
        /// <inheritdoc/>
        public event BinaryMessageReceivedEventHandler? BinaryMessageReceived;

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

        /// <summary>
        /// A value indicating whether the client is disconnecting.
        /// </summary>
        private bool _clientIsDisconnecting;

        /// <summary>
        /// The logger to write internal log messages.
        /// </summary>
        private readonly ILogger? _logger = logger;

        /// <inheritdoc/>
        public async Task ConnectAsync(CancellationToken? cancellationToken = null)
        {
            if (IsConnected) throw new WebSocketClientException(message: "Client is already connected");
            cancellationToken ??= CancellationToken.None;

            _cancellationTokenSource = new CancellationTokenSource();
            var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken.Value, _cancellationTokenSource.Token);

            _logger?.LogInformation("Connecting to Server");
            try
            {
                _client = new TcpClientWrapper();
                await _client.ConnectAsync(HostName, Port);
                await HandleWebSocketInitiation(_client, linkedTokenSource.Token);

                _logger?.LogDebug("Connection upgraded, now listening.");
                _ = ProcessWebSocketMessagesAsync(_webSocket!, linkedTokenSource.Token);
            }
            catch (Exception exception)
            {
                _logger?.LogError(exception, "Error connecting to Server");
                if (exception is WebSocketException)
                {
                    throw;
                }
                else
                {
                    throw new WebSocketClientException(message: "Error connecting to Server", innerException: exception);
                }
            }
        }

        /// <inheritdoc/>
        public async Task DisconnectAsync(string closingStatusDescription = "Closing", CancellationToken? cancellationToken = null)
        {
            if (_clientIsDisconnecting) throw new WebSocketClientException("Client is already disconnecting");
            _clientIsDisconnecting = true;

            cancellationToken ??= CancellationToken.None;
            var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken.Value, _cancellationTokenSource.Token);

            _logger?.LogInformation("Disconnecting from Server");

            if (_webSocket != null && (_webSocket.State == WebSocketState.Open || _webSocket.State == WebSocketState.CloseReceived))
            {
                try
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, closingStatusDescription, linkedTokenSource.Token);
                }
                catch (Exception exception)
                {
                    _logger?.LogError(exception, "Error closing WebSocket");
                    if (exception is WebSocketException)
                    {
                        throw;
                    }
                    else
                    {
                        throw new WebSocketClientException(message: "Error closing WebSocket", innerException: exception);
                    }
                }
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
            var socketWrapper = new WebSocketUpgradeHandler(_stream);

            var requestContext = WebContext.CreateRequest(HostName, Port, RequestPath, userId);
            await socketWrapper.SendUpgradeRequestAsync(requestContext, cancellationToken);
            var response = await socketWrapper.AwaitContextAsync(cancellationToken);
            WebSocketUpgradeHandler.ValidateUpgradeResponse(response, requestContext);

            if (response.ContainsUserId)
            {
                UserId = response.UserId;
            }

            _webSocket = socketWrapper.CreateWebSocket(isServer: false);
        }

        /// <inheritdoc/>
        public async Task SendMessageAsync(string message, CancellationToken? cancellationToken = null)
        {
            if (!IsConnected) throw new WebSocketClientException(message: "Client is not connected");
            if (_webSocket == null) throw new WebSocketClientException(message: "WebSocket is not initialized");

            cancellationToken ??= CancellationToken.None;
            var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken.Value, _cancellationTokenSource.Token);

            try
            {
                // Send the message
                var buffer = Encoding.UTF8.GetBytes(message);
                await _webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, linkedTokenSource.Token);
                _logger?.LogDebug("Message sent: {message}", message);
            }
            catch (Exception exception)
            {
                throw new WebSocketClientException(message: "Error sending message", innerException: exception);
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
                    _logger?.LogDebug("Message received: {message}", receivedMessage);
                    _ = Task.Run(() => MessageReceived?.Invoke(this, new MessageReceivedArgs(receivedMessage)), cancellationToken);
                }
                else if (result.MessageType == WebSocketMessageType.Binary)
                {
                    // Handle the binary message
                    _logger?.LogDebug("Binary message received, length: {length} bytes", result.Count);
                    _ = Task.Run(() => BinaryMessageReceived?.Invoke(this, new BinaryMessageReceivedArgs(buffer[..result.Count])), cancellationToken);
                }
                // We have to check if the client is disconnecting here,
                // because then we already sent the close message and we don't want to send another one
                else if (result.MessageType == WebSocketMessageType.Close && !_clientIsDisconnecting)
                {
                    _logger?.LogInformation("Received close message from server");
                    _ = Task.Run(() => Disconnected?.Invoke(this, new DisconnectedArgs(result.CloseStatusDescription ?? string.Empty)), cancellationToken);
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