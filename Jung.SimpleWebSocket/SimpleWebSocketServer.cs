// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket.Contracts;
using Jung.SimpleWebSocket.Exceptions;
using Jung.SimpleWebSocket.Wrappers;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.WebSockets;
using System.Text;

namespace Jung.SimpleWebSocket
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleWebSocketServer"/> class that listens
    /// for incoming connection attempts on the specified local IP address and port number.
    /// </summary>
    public class SimpleWebSocketServer : SimpleWebSocketBase, IWebSocketServer, IDisposable
    {
        /// <inheritdoc/>
        public event Action<object?>? ClientConnected;

        /// <inheritdoc/>
        public IPAddress LocalIpAddress { get; }
        /// <inheritdoc/>
        public int Port { get; }

        /// <inheritdoc/>
        public bool IsListening => _server?.IsListening ?? false;

        private CancellationTokenSource _cancellationTokenSource = new();
        private ITcpListener? _server;
        private IWebSocket? _webSocket;

        /// <param name="localIpAddress">A local ip address</param>
        /// <param name="port">A port on which to listen for incoming connection attempts</param>
        /// <param name="logger">A logger to write internal log messages</param>
        public SimpleWebSocketServer(IPAddress localIpAddress, int port, ILogger? logger = null)
            : base(logger)
        {
            LocalIpAddress = localIpAddress;
            Port = port;
        }

        /// <summary>
        /// Constructor for dependency injection (used in tests)
        /// </summary>
        /// <param name="localIpAddress">A local ip address</param>
        /// <param name="port">A port on which to listen for incoming connection attempts</param>
        /// <param name="tcpListener">A wrapped tcp listener</param>
        /// <param name="logger">>A logger to write internal log messages</param>
        internal SimpleWebSocketServer(IPAddress localIpAddress, int port, ITcpListener tcpListener, ILogger? logger = null)
            : base(logger)
        {
            LocalIpAddress = localIpAddress;
            Port = port;
            _server = tcpListener;
        }

        /// <inheritdoc/>
        public void Start(CancellationToken cancellation)
        {
            if(IsListening) throw new WebSocketServerException("Server is already started");

            _cancellationTokenSource = new CancellationTokenSource();
            var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _cancellationTokenSource.Token);

            _server ??= new TcpListenerWrapper(LocalIpAddress, Port);
            _server.Start();
            _ = Task.Run(async delegate
            {
                LogInternal($"Server started at {LocalIpAddress}:{Port}");
                while (!linkedTokenSource.IsCancellationRequested)
                {
                    try
                    {
                        // Accept the client
                        var client = await _server.AcceptTcpClientAsync(linkedTokenSource.Token);

                        LogInternal("Client connected", $"Client connected from {client.RemoteEndPoint}");
                        await HandleClientAsync(client, linkedTokenSource.Token);
                    }
                    catch (Exception exception)
                    {
                        _logger?.LogError(exception, "Error accepting _client");
                    }
                }
            }, linkedTokenSource.Token);
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

        private async Task HandleClientAsync(ITcpClient client, CancellationToken cancellationToken)
        {
            // Upgrade the connection to a WebSocket
            using var stream = client.GetStream();
            var socketWrapper = new SocketWrapper(stream);
            var request = await socketWrapper.AwaitContextAsync(cancellationToken);
            await socketWrapper.AcceptWebSocketAsync(request, cancellationToken);
            _webSocket = socketWrapper.CreateWebSocket(true);

            _ = Task.Run(() => ClientConnected?.Invoke(null), cancellationToken);

            // Start listening for messages
            LogInternal("Connection upgraded, now listening.");
            await ProcessWebSocketMessagesAsync(_webSocket, cancellationToken);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _server?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}