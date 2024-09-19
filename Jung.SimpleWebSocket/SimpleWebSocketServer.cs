// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket.Contracts;
using Jung.SimpleWebSocket.Exceptions;
using Jung.SimpleWebSocket.Models;
using Jung.SimpleWebSocket.Models.EventArguments;
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
        public IPAddress LocalIpAddress { get; }
        /// <inheritdoc/>
        public int Port { get; }

        /// <inheritdoc/>
        public event Action<ClientConnectedArgs>? ClientConnected;
        /// <inheritdoc/>
        public event Action<ClientDisconnectedArgs>? ClientDisconnected;
        /// <inheritdoc/>
        public event Action<ClientMessageReceivedArgs>? MessageReceived;
        /// <inheritdoc/>
        public event Action<ClientBinaryMessageReceivedArgs>? BinaryMessageReceived;

        /// <summary>
        /// A dictionary of active clients.
        /// </summary>
        private Dictionary<string, WebSocketServerClient> ActiveClients { get; } = [];

        /// <inheritdoc />
        public string[] ClientIds => [.. ActiveClients.Keys];

        /// <inheritdoc />
        public int ClientCount => ActiveClients.Count;

        /// <summary>
        /// Future: Handle passive (disconnected) clients, delete them after a period of time, configurate this behavior in the WebSocketServerOptions
        /// </summary>
        private Dictionary<string, WebSocketServerClient> PassiveClients { get; } = [];

        /// <inheritdoc/>
        public bool IsListening => _server?.IsListening ?? false;

        /// <summary>
        /// A flag indicating whether the server is started.
        /// </summary>
        private bool _isStarted;

        /// <summary>
        /// A cancellation token source to cancel the server.
        /// </summary>
        private CancellationTokenSource _cancellationTokenSource = new();

        /// <summary>
        /// The server that listens for incoming connection attempts.
        /// </summary>
        private ITcpListener? _server;

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
        public void Start(CancellationToken? cancellationToken = null)
        {
            if (_isStarted) throw new WebSocketServerException("Server is already started");
            _isStarted = true;
            cancellationToken ??= CancellationToken.None;

            _cancellationTokenSource = new CancellationTokenSource();
            var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken.Value, _cancellationTokenSource.Token);

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

                        LogInternal("Client connected", $"Client connected from {client.ClientConnection.RemoteEndPoint}");
                        ActiveClients.Add(client.Id, client);
                        _ = HandleClientAsync(client, linkedTokenSource.Token);
                    }
                    catch (Exception exception)
                    {
                        _logger?.LogError(exception, "Error accepting _client");
                    }
                }
            }, linkedTokenSource.Token);
        }

        /// <inheritdoc/>
        public async Task SendMessageAsync(string clientId, string message, CancellationToken? cancellationToken = null)
        {
            // Find and check the client
            if (!ActiveClients.TryGetValue(clientId, out var client)) throw new WebSocketServerException(message: "Client not found");
            if (client.WebSocket == null) throw new WebSocketServerException(message: "Client is not connected");
            cancellationToken ??= CancellationToken.None;

            try
            {
                // Send the message
                var buffer = Encoding.UTF8.GetBytes(message);
                await client.WebSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, cancellationToken.Value);
                LogInternal("Message sent", $"Message sent: \"{message}\"");
            }
            catch (Exception exception)
            {
                throw new WebSocketServerException(message: "Error sending message", innerException: exception);
            }
        }

        /// <inheritdoc/>
        /// <exception cref="WebSocketServerException"></exception>
        public WebSocketServerClient GetClientById(string clientId)
        {
            if (!ActiveClients.TryGetValue(clientId, out var client)) throw new WebSocketServerException(message: "Client not found");
            return client;
        }

        /// <summary>
        /// Handles the client connection.
        /// </summary>
        /// <param name="client">The client to handle</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>A asynchronous task</returns>
        private async Task HandleClientAsync(WebSocketServerClient client, CancellationToken cancellationToken)
        {
            try
            {
                // Upgrade the connection to a WebSocket
                using var stream = client.ClientConnection.GetStream();
                var socketWrapper = new SocketWrapper(stream);
                var request = await socketWrapper.AwaitContextAsync(cancellationToken);
                await socketWrapper.AcceptWebSocketAsync(request, cancellationToken);
                client.UpdateWebSocket(socketWrapper.CreateWebSocket(isServer: true));

                _ = Task.Run(() => ClientConnected?.Invoke(new ClientConnectedArgs(client.Id)), cancellationToken);

                // Start listening for messages
                LogInternal("Connection upgraded, now listening.");
                await ProcessWebSocketMessagesAsync(client, cancellationToken);
            }
            catch (Exception exception)
            {
                LogInternal("An Error occurred while handling the client", $"An Error occurred while handling the client: {exception}");
            }
            finally
            {
                ActiveClients.Remove(client.Id);
            }
        }

        /// <summary>
        /// Processes the WebSocket messages.
        /// </summary>
        /// <param name="client">The client whose messages to process</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>A asynchronous task</returns>
        /// <exception cref="InvalidOperationException"></exception>
        private async Task ProcessWebSocketMessagesAsync(WebSocketServerClient client, CancellationToken cancellationToken)
        {
            if (client.WebSocket == null)
            {
                throw new InvalidOperationException("WebSocket is not initialized");
            }

            var webSocket = client.WebSocket;

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
                    _ = Task.Run(() => MessageReceived?.Invoke(new ClientMessageReceivedArgs(receivedMessage, client.Id)), cancellationToken);
                }
                else if (result.MessageType == WebSocketMessageType.Binary)
                {
                    // Handle the binary message
                    LogInternal($"Binary message received", $"Binary message received, length: {result.Count} bytes");
                    _ = Task.Run(() => BinaryMessageReceived?.Invoke(new ClientBinaryMessageReceivedArgs(buffer[..result.Count], client.Id)), cancellationToken);
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    LogInternal("WebSocket closed");
                    _ = Task.Run(() => ClientDisconnected?.Invoke(new ClientDisconnectedArgs(result.CloseStatusDescription ?? string.Empty, client.Id)), cancellationToken);
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
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
    }
}