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
        public bool IsConnected => _client?.IsConnected ?? false;

        private CancellationTokenSource _cancellationTokenSource = new();
        private TcpClientWrapper? _client;
        private IWebSocket? _webSocket;
        private INetworkStream? _stream;

        /// <inheritdoc/>
        public async Task ConnectAsync(CancellationToken cancellation)
        {
            if(IsConnected) throw new WebSocketServerException(message: "Client is already connected");

            _cancellationTokenSource = new CancellationTokenSource();
            var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _cancellationTokenSource.Token);

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
        public async Task DisconnectAsync(CancellationToken cancellationToken)
        {
            if (_webSocket != null && (_webSocket.State == WebSocketState.Open || _webSocket.State == WebSocketState.CloseReceived))
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", cancellationToken);
            }
            _client?.Dispose();
        }

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