// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket.Contracts;
using Jung.SimpleWebSocket.Delegates;
using Jung.SimpleWebSocket.Exceptions;
using Jung.SimpleWebSocket.Flows;
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

namespace Jung.SimpleWebSocket
{
    /// <summary>
    /// A simple WebSocket server.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="SimpleWebSocketServer"/> class that listens
    /// for incoming connection attempts on the specified local IP address and port number.
    /// </remarks>
    /// <param name="options">The options for the server</param>
    /// <param name="logger">A logger to write internal log messages</param>
    public class SimpleWebSocketServer(SimpleWebSocketServerOptions options, ILogger? logger = null) : IWebSocketServer, IDisposable
    {
        /// <inheritdoc/>
        public IPAddress LocalIpAddress { get; } = options.LocalIpAddress;
        /// <inheritdoc/>
        public int Port { get; } = options.Port;

        /// <inheritdoc/>
        public event EventHandler<ClientConnectedArgs>? ClientConnected;
        /// <inheritdoc/>
        public event EventHandler<ClientDisconnectedArgs>? ClientDisconnected;
        /// <inheritdoc/>
        public event EventHandler<ClientMessageReceivedArgs>? MessageReceived;
        /// <inheritdoc/>
        public event EventHandler<ClientBinaryMessageReceivedArgs>? BinaryMessageReceived;

        /// <inheritdoc/>
        public event AsyncEventHandler<ClientUpgradeRequestReceivedArgs>? ClientUpgradeRequestReceivedAsync;

        /// <summary>
        /// A dictionary of active clients.
        /// </summary>
        internal ConcurrentDictionary<string, WebSocketServerClient> ActiveClients { get; } = [];

        /// <inheritdoc />
        public string[] ClientIds => [.. ActiveClients.Keys];

        /// <inheritdoc />
        public int ClientCount => ActiveClients.Count;

        /// <inheritdoc/>
        public bool IsListening => _tcpListener?.IsListening ?? false;

        /// <summary>
        /// A logger to write internal log messages.
        /// </summary>
        internal ILogger? Logger { get; } = logger;

        /// <summary>
        /// The options for the server.
        /// </summary>
        internal SimpleWebSocketServerOptions Options { get; } = options;

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
        private ITcpListener? _tcpListener;

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
            _tcpListener = tcpListener;
        }

        /// <inheritdoc/>
        public void Start(CancellationToken? cancellationToken = null)
        {
            if (_isStarted) throw new WebSocketServerException("Server is already started");
            _isStarted = true;
            cancellationToken ??= CancellationToken.None;

            _cancellationTokenSource = new CancellationTokenSource();
            var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken.Value, _cancellationTokenSource.Token);

            _tcpListener ??= new TcpListenerWrapper(LocalIpAddress, Port);
            _tcpListener.Start();
            _ = Task.Run(async delegate
            {
                Logger?.LogInformation("Server started at {LocalIpAddress}:{Port}", LocalIpAddress, Port);
                while (!linkedTokenSource.IsCancellationRequested)
                {
                    try
                    {
                        // Accept the client
                        var client = await _tcpListener.AcceptTcpClientAsync(linkedTokenSource.Token);

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
            _tcpListener?.Dispose();
            _tcpListener = null;
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

        /// <inheritdoc/>
        public void ChangeClientId(WebSocketServerClient client, string newId)
        {
            // if the client is not found or the new id is already in use, throw an exception
            if (!ActiveClients.TryGetValue(client.Id, out var _)) throw new ClientNotFoundException(message: "A client with the given id was not found");
            if (ActiveClients.ContainsKey(newId)) throw new ClientIdAlreadyExistsException(message: "A client with the new id already exists");

            // because the id is used as a key in the dictionary,
            // we have to remove the client and add it again with the new id
            ActiveClients.TryRemove(client.Id, out _);
            client.UpdateId(newId);
            ActiveClients.TryAdd(newId, client);
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

        /// <inheritdoc/>
        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _tcpListener?.Dispose();
            _tcpListener = null;
            GC.SuppressFinalize(this);
        }
    }
}