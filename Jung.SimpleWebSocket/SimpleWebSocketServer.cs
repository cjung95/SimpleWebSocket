// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket.Contracts;
using Jung.SimpleWebSocket.Delegates;
using Jung.SimpleWebSocket.Exceptions;
using Jung.SimpleWebSocket.Models;
using Jung.SimpleWebSocket.Models.EventArguments;
using Jung.SimpleWebSocket.Utility;
using Jung.SimpleWebSocket.Wrappers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;

namespace Jung.SimpleWebSocket
{
    /// <summary>
    /// A simple WebSocket server.
    /// </summary>
    public class SimpleWebSocketServer : IWebSocketServer, IDisposable
    {
        /// <inheritdoc/>
        public IPAddress LocalIpAddress { get; }
        /// <inheritdoc/>
        public int Port { get; }

        /// <inheritdoc/>
        public event EventHandler<ClientConnectedArgs>? ClientConnected;
        /// <inheritdoc/>
        public event EventHandler<ClientDisconnectedArgs>? ClientDisconnected;
        /// <inheritdoc/>
        public event EventHandler<ClientMessageReceivedArgs>? MessageReceived;
        /// <inheritdoc/>
        public event EventHandler<ClientBinaryMessageReceivedArgs>? BinaryMessageReceived;
        /// <inheritdoc/>
        public event EventHandler<PassiveUserExpiredArgs>? PassiveUserExpiredEvent;

        /// <inheritdoc/>
        public event AsyncEventHandler<ClientUpgradeRequestReceivedArgs>? ClientUpgradeRequestReceivedAsync;

        /// <summary>
        /// A dictionary of active clients.
        /// </summary>
        internal ConcurrentDictionary<string, WebSocketServerClient> ActiveClients { get; } = [];

        /// <summary>
        /// A dictionary of passive clients.
        /// </summary>
        internal IDictionary<string, WebSocketServerClient> PassiveClients { get; set; } = null!;

        /// <inheritdoc />
        public string[] ClientIds => [.. ActiveClients.Keys];

        /// <inheritdoc />
        public int ClientCount => ActiveClients.Count;

        /// <inheritdoc/>
        public bool IsListening => _server?.IsListening ?? false;

        /// <summary>
        /// A logger to write internal log messages.
        /// </summary>
        internal ILogger? Logger { get; }

        /// <summary>
        /// The options for the server.
        /// </summary>
        internal SimpleWebSocketServerOptions Options { get; }

        /// <summary>
        /// A flag indicating whether the server is started.
        /// </summary>
        private bool _isStarted;

        /// <summary>
        /// A flag indicating whether the server is shutting down.
        /// </summary>
        private bool _serverShuttingDown;

        /// <summary>
        /// A cancellation token source to cancel the server.
        /// </summary>
        private CancellationTokenSource _cancellationTokenSource = new();

        /// <summary>
        /// The server that listens for incoming connection attempts.
        /// </summary>
        private ITcpListener? _server;

        /// <summary>
        /// Initializes a new instance of the <see cref="SimpleWebSocketServer"/> class that listens
        /// for incoming connection attempts on the specified local IP address and port number.
        /// </summary>
        /// <param name="options">The options for the server</param>
        /// <param name="logger">A logger to write internal log messages</param>
        public SimpleWebSocketServer(SimpleWebSocketServerOptions options, ILogger? logger = null)
        {
            LocalIpAddress = options.LocalIpAddress;
            Port = options.Port;
            Logger = logger;
            Options = options;
            InitializePassiveClientDictionary(options);
        }

        /// <summary>
        /// Initializes the passive clients dictionary.
        /// </summary>
        /// <param name="options"></param>
        private void InitializePassiveClientDictionary(SimpleWebSocketServerOptions options)
        {
            if (options.RememberDisconnectedClients)
            {
                // Initialize the passive clients dictionary
                if (options.RemovePassiveClientsAfterClientExpirationTime)
                {
                    var passiveClients = new ExpiringDictionary<string, WebSocketServerClient>(options.PassiveClientLifetime, Logger);
                    passiveClients.ItemExpired += PassiveClients_ItemExpired;
                    PassiveClients = passiveClients;
                }
                else
                {
                    PassiveClients = new Dictionary<string, WebSocketServerClient>();
                }
            }
            else
            {
                // If user handling is not activated, the passive clients are not needed
                PassiveClients = null!;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SimpleWebSocketServer"/> class that listens
        /// for incoming connection attempts on the specified local IP address and port number.
        /// </summary>
        /// <param name="options">The options for the server</param>
        /// <param name="logger">A logger to write internal log messages</param>
        public SimpleWebSocketServer(IOptions<SimpleWebSocketServerOptions> options, ILogger? logger = null)
            : this(options.Value, logger)
        {
        }

        /// <summary>
        /// Constructor for dependency injection (used in tests)
        /// </summary>
        /// <param name="options">The options for the server</param>
        /// <param name="tcpListener">A wrapped tcp listener</param>
        /// <param name="logger">>A logger to write internal log messages</param>
        internal SimpleWebSocketServer(SimpleWebSocketServerOptions options, ITcpListener tcpListener, ILogger? logger = null)
            : this(options, logger)
        {
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
                Logger?.LogInformation("Server started at {LocalIpAddress}:{Port}", LocalIpAddress, Port);
                while (!linkedTokenSource.IsCancellationRequested)
                {
                    try
                    {
                        // Accept the client
                        var client = await _server.AcceptTcpClientAsync(linkedTokenSource.Token);

                        Logger?.LogDebug("Client connected from {endpoint}", client.ClientConnection!.RemoteEndPoint);

                        _ = HandleClientAsync(client, linkedTokenSource.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        // Ignore the exception, because it is thrown when cancellation is requested
                    }
                    catch (Exception exception)
                    {
                        Logger?.LogError(exception, "Error while accepting Client.");
                    }
                }
            }, linkedTokenSource.Token);
        }

        /// <inheritdoc/>
        public async Task ShutdownServer(CancellationToken? cancellationToken = null)
        {
            if (!_isStarted) throw new WebSocketServerException("Server is not started");
            if (_serverShuttingDown) throw new WebSocketServerException("Server is already shutting down");
            _serverShuttingDown = true;

            cancellationToken ??= CancellationToken.None;
            var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken.Value, _cancellationTokenSource.Token);

            Logger?.LogInformation("Stopping server...");

            // copying the active clients to avoid a collection modified exception
            var activeClients = ActiveClients.Values.ToArray();
            foreach (var client in activeClients)
            {
                if (client.WebSocket != null && client.WebSocket.State == WebSocketState.Open)
                {
                    await client.WebSocket.CloseAsync(WebSocketCloseStatus.EndpointUnavailable, "Server is shutting down", linkedTokenSource.Token);
                    ActiveClients.TryRemove(client.Id, out _);
                    client?.Dispose();
                }
            }

            _cancellationTokenSource?.Cancel();
            _server?.Dispose();
            _server = null;
            Logger?.LogInformation("Server stopped");
        }

        /// <inheritdoc/>
        public async Task SendMessageAsync(string clientId, string message, CancellationToken? cancellationToken = null)
        {
            // Find and check the client
            if (!ActiveClients.TryGetValue(clientId, out var client)) throw new WebSocketServerException(message: "Client not found");
            if (client.WebSocket == null) throw new WebSocketServerException(message: "Client is not connected");

            cancellationToken ??= CancellationToken.None;
            var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken.Value, _cancellationTokenSource.Token);

            try
            {
                // Send the message
                var buffer = Encoding.UTF8.GetBytes(message);
                await client.WebSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, linkedTokenSource.Token);
                Logger?.LogDebug("Message sent: {message}.", message);
            }
            catch (Exception exception)
            {
                Logger?.LogError(exception, "Error while sending a message.");
                throw new WebSocketServerException(message: "An Error occurred sending a message.", innerException: exception);
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
            var flow = new ClientHandlingFlow(this, client, cancellationToken);
            try
            {
                // Load the request context 
                await flow.LoadRequestContext();

                // Handle the client user identification if activated
                flow.HandleClientIdentification();

                // raise async client upgrade request received event
                var eventArgs = await flow.RaiseUpgradeEventAsync(ClientUpgradeRequestReceivedAsync);

                // Respond to the upgrade request
                if (eventArgs.Handle)
                {
                    // Accept the WebSocket connection
                    await flow.AcceptWebSocketAsync();
                    if (flow.TryAddClientToActiveUserList())
                    {
                        Logger?.LogDebug("Connection upgraded, now listening on Client {clientId}", flow.Client.Id);
                        AsyncEventRaiser.RaiseAsyncInNewTask(ClientConnected, this, new ClientConnectedArgs(flow.Client.Id), cancellationToken);
                        // Start listening for messages
                        await ProcessWebSocketMessagesAsync(flow.Client, cancellationToken);
                    }
                    else
                    {
                        Logger?.LogDebug("Connection upgraded, now listening on Client {clientId}", flow.Client.Id);
                    }
                }
                else
                {
                    // Reject the WebSocket connection
                    Logger?.LogDebug("Client upgrade request rejected by ClientUpgradeRequestReceivedAsync event.");
                    await flow.RejectWebSocketAsync(eventArgs.ResponseContext);
                }
            }
            catch (OperationCanceledException)
            {
                // Ignore the exception, because it is thrown when cancellation is requested
            }
            catch (UserNotHandledException userNotHandledException)
            {
                await flow.RejectWebSocketAsync(userNotHandledException.ResponseContext);
            }
            catch (Exception exception)
            {
                Logger?.LogError(exception, "Error while handling the Client {clientId}", flow.Client.Id);
            }
            finally
            {
                // If the client was added and the server is not shutting down, handle the disconnected client
                // The client is not added if the connection was rejected
                if (!_serverShuttingDown)
                {
                    flow.HandleDisconnectedClient();
                }
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
                cancellationToken.ThrowIfCancellationRequested();
                // Read the next message
                WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    // Handle the text message
                    string receivedMessage = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    Logger?.LogDebug("Message received: {message}", receivedMessage);
                    AsyncEventRaiser.RaiseAsyncInNewTask(MessageReceived, this, new ClientMessageReceivedArgs(receivedMessage, client.Id), cancellationToken);
                }
                else if (result.MessageType == WebSocketMessageType.Binary)
                {
                    // Handle the binary message
                    Logger?.LogDebug("Binary message received, length: {length} bytes", result.Count);
                    AsyncEventRaiser.RaiseAsyncInNewTask(BinaryMessageReceived, this, new ClientBinaryMessageReceivedArgs(buffer[..result.Count], client.Id), cancellationToken);
                }
                // We have to check if the is shutting down here,
                // because then we already sent the close message and we don't want to send another one
                else if (result.MessageType == WebSocketMessageType.Close && !_serverShuttingDown)
                {
                    Logger?.LogInformation("Received close message from Client");
                    AsyncEventRaiser.RaiseAsyncInNewTask(ClientDisconnected, this, new ClientDisconnectedArgs(result.CloseStatusDescription ?? string.Empty, client.Id), cancellationToken);
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    break;
                }
            }
        }

        /// <summary>
        /// Handles the event when a passive user expired.
        /// </summary>
        /// <remarks>
        /// Condition: <see cref="SimpleWebSocketServerOptions.RemovePassiveClientsAfterClientExpirationTime"/> is set to <c>true</c>.
        /// </remarks>
        /// <param name="sender">The sender of the event (<see cref="PassiveClients"/>)</param>
        /// <param name="e">The arguments of the event</param>
        private void PassiveClients_ItemExpired(object? sender, ItemExpiredArgs<WebSocketServerClient> e)
        {
            Logger?.LogDebug("Passive Client expired: {clientId}", e.Item.Id);

            // Raise the event asynchronously
            // We don't want to block the cleanup process
            AsyncEventRaiser.RaiseAsyncInNewTask(PassiveUserExpiredEvent, this, new PassiveUserExpiredArgs(e.Item.Id), _cancellationTokenSource.Token);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _server?.Dispose();
            _server = null;
            GC.SuppressFinalize(this);
        }
    }
}