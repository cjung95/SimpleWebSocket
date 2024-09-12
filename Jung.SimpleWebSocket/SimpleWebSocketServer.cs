using Jung.SimpleWebSocket.Contracts;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;

namespace Jung.SimpleWebSocket
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleWebSocketServer"/> class that listens
    /// for incoming connection attempts on the specified local IP address and port number.
    /// </summary>
    /// <param name="localIpAddress">A local ip address</param>
    /// <param name="port">A port on which to listen for incoming connection attempts</param>
    /// <param name="logger">A logger to write internal log messages</param>
    public class SimpleWebSocketServer(IPAddress localIpAddress, int port, ILogger? logger = null) : IWebSocketServer, IDisposable
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
        public IPAddress LocalIpAddress { get; } = localIpAddress;
        /// <inheritdoc/>
        public int Port { get; } = port;

        private CancellationTokenSource _cancellationTokenSource = new();
        private TcpListener? _server;
        private WebSocket? _webSocket;
        private readonly ILogger? _logger = logger;

        /// <inheritdoc/>
        public async Task StartAsync(CancellationToken cancellation)
        {
            _cancellationTokenSource = new CancellationTokenSource();
            var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _cancellationTokenSource.Token);

            _server = new TcpListener(LocalIpAddress, Port);
            _server.Start();

            LogInternal($"Server started at {LocalIpAddress}:{Port}");
            while (!linkedTokenSource.IsCancellationRequested)
            {
                try
                {
                    // Accept the client
                    var client = await _server.AcceptTcpClientAsync(linkedTokenSource.Token);

                    LogInternal("Client connected", $"Client connected from {client.Client.RemoteEndPoint}");
                    await HandleClientAsync(client, linkedTokenSource.Token);
                }
                catch (Exception exception)
                {
                    _logger?.LogError(exception, "Error accepting _client");
                }
            }
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

        private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            // Upgrade the connection to a WebSocket
            using var stream = client.GetStream();
            var socketWrapper = new SocketWrapper(stream);
            var request = await socketWrapper.AwaitContextAsync(cancellationToken);
            _webSocket = await socketWrapper.AcceptWebSocketAsync(request, cancellationToken);

            _ = Task.Run(() => ClientConnected?.Invoke(null), cancellationToken);

            // Start listening for messages
            LogInternal("Connection upgraded, now listening.");
            await ProcessWebSocketMessagesAsync(_webSocket, cancellationToken);
        }

        private async Task ProcessWebSocketMessagesAsync(WebSocket ws, CancellationToken cancellationToken)
        {
            var buffer = new byte[1024 * 4]; // Buffer for incoming data
            while (ws.State == WebSocketState.Open)
            {
                // Read the next message
                WebSocketReceiveResult result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

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
                    LogInternal("WebSocket closed by client");
                    _ = Task.Run(() => ClientDisconnected?.Invoke(null), cancellationToken);
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    break;
                }
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _server?.Dispose();
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

