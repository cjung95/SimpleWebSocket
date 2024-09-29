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
using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;

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
        public event ClientConnectedEventHandler? ClientConnected;
        /// <inheritdoc/>
        public event ClientDisconnectedEventHandler? ClientDisconnected;
        /// <inheritdoc/>
        public event ClientMessageReceivedEventHandler? MessageReceived;
        /// <inheritdoc/>
        public event ClientBinaryMessageReceivedEventHandler? BinaryMessageReceived;
        /// <inheritdoc/>
        public event PassiveUserExpiredEventHandler? PassiveUserExpiredEvent;

        /// <inheritdoc/>
        public event AsyncEventHandler<ClientUpgradeRequestReceivedArgs>? ClientUpgradeRequestReceivedAsync;

        /// <summary>
        /// A dictionary of active clients.
        /// </summary>
        private ConcurrentDictionary<string, WebSocketServerClient> ActiveClients { get; } = [];

        /// <summary>
        /// A dictionary of passive clients.
        /// </summary>
        /// <remarks>
        /// The passive clients get removed after a certain time of inactivity.
        /// </remarks>
        private ExpiringDictionary<string, WebSocketServerClient> PassiveClients { get; }

        /// <inheritdoc />
        public string[] ClientIds => [.. ActiveClients.Keys];

        /// <inheritdoc />
        public int ClientCount => ActiveClients.Count;

        /// <inheritdoc/>
        public bool IsListening => _server?.IsListening ?? false;

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
        /// A logger to write internal log messages.
        /// </summary>
        private readonly ILogger? _logger;

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
            _logger = logger;
            PassiveClients = new(options.PassiveClientLifetime, _logger);
            PassiveClients.ItemExpired += PassiveClients_ItemExpired;
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
                _logger?.LogInformation("Server started at {LocalIpAddress}:{Port}", LocalIpAddress, Port);
                while (!linkedTokenSource.IsCancellationRequested)
                {
                    try
                    {
                        // Accept the client
                        var client = await _server.AcceptTcpClientAsync(linkedTokenSource.Token);

                        _logger?.LogDebug("Client connected from {endpoint}", client.ClientConnection!.RemoteEndPoint);

                        _ = HandleClientAsync(client, linkedTokenSource.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        // Ignore the exception, because it is thrown when cancellation is requested
                    }
                    catch (Exception exception)
                    {
                        _logger?.LogError(exception, "Error while accepting client.");
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

            _logger?.LogInformation("Stopping server...");

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
            _logger?.LogInformation("Server stopped");
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
                _logger?.LogDebug("Message sent: {message}.", message);
            }
            catch (Exception exception)
            {
                _logger?.LogError(exception, "Error while sending a message.");
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
            bool clientAdded = false;
            try
            {
                // Upgrade the connection to a WebSocket
                using var stream = client.ClientConnection!.GetStream();
                var upgradeHandler = new WebSocketUpgradeHandler(stream);
                var request = await upgradeHandler.AwaitContextAsync(cancellationToken);

                // Check if the request contains a user id
                if (request.ContainsUserId)
                {
                    _logger?.LogDebug("User id found in request: {userId}", request.UserId);
                    // Check if the client is an existing passive client
                    var clientExists = PassiveClients.ContainsKey(request.UserId);
                    if (clientExists)
                    {
                        _logger?.LogDebug("Passive client found for user id {userId} - reactivating user.", request.UserId);

                        // Use the existing client
                        // Update the client with the new connection
                        // Remove the client from the passive clients
                        var passiveClient = PassiveClients[request.UserId];
                        passiveClient.UpdateClient(client.ClientConnection);
                        client = passiveClient;
                        PassiveClients.Remove(request.UserId);
                    }
                    else
                    {
                        // No passive client found, checking for active clients with the same id
                        if (ActiveClients.ContainsKey(request.UserId))
                        {
                            _logger?.LogDebug("Active client found for user id {userId} - rejecting connection.", request.UserId);
                            // Reject the connection

                            var responseContext = new WebContext
                            {
                                StatusCode = HttpStatusCode.Conflict,
                                BodyContent = "User id already in use"
                            };
                            await upgradeHandler.RejectWebSocketAsync(responseContext, cancellationToken);
                            return;
                        }
                        else
                        {
                            // Update the client with the new id
                            client.UpdateId(request.UserId);
                        }
                    }
                }

                // raise async client upgrade request received event
                var eventArgs = new ClientUpgradeRequestReceivedArgs(client, request, _logger);
                await AsyncEventRaiser.RaiseAsync(ClientUpgradeRequestReceivedAsync, this, eventArgs, cancellationToken);
                if (eventArgs.Handle)
                {
                    // The client is accepted
                    await upgradeHandler.AcceptWebSocketAsync(request, eventArgs.ResponseContext, client.Id, null, cancellationToken);

                    // Update the client with the new WebSocket
                    client.UseWebSocket(upgradeHandler.CreateWebSocket(isServer: true));

                    clientAdded = ActiveClients.TryAdd(client.Id, client);
                    if (clientAdded)
                    {
                        _ = Task.Run(() => ClientConnected?.Invoke(this, new ClientConnectedArgs(client.Id)), cancellationToken);
                        // Start listening for messages
                        _logger?.LogDebug("Connection upgraded, now listening on client {clientId}", client.Id);
                        await ProcessWebSocketMessagesAsync(client, cancellationToken);
                    }
                }
                else
                {
                    // The client is rejected
                    _logger?.LogDebug("Client upgrade request rejected by ClientUpgradeRequestReceivedAsync event.");
                    await upgradeHandler.RejectWebSocketAsync(eventArgs.ResponseContext, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Ignore the exception, because it is thrown when cancellation is requested
            }
            catch (Exception exception)
            {
                _logger?.LogError(exception, "Error while handling the client {clientId}", client.Id);
            }
            finally
            {
                // If the client was added and the server is not shutting down, handle the disconnected client
                // The client is not added if the connection was rejected
                if (clientAdded && !_serverShuttingDown)
                {
                    HandleDisconnectedClient(client);
                }
            }
        }

        private void HandleDisconnectedClient(WebSocketServerClient client)
        {
            ActiveClients.TryRemove(client.Id, out _);
            client.Dispose();

            _logger?.LogDebug("Client {clientId} is now a passive user.", client.Id);
            PassiveClients.Add(client.Id, client);
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
                    _logger?.LogDebug("Message received: {message}", receivedMessage);
                    _ = Task.Run(() => MessageReceived?.Invoke(this, new ClientMessageReceivedArgs(receivedMessage, client.Id)), cancellationToken);
                }
                else if (result.MessageType == WebSocketMessageType.Binary)
                {
                    // Handle the binary message
                    _logger?.LogDebug("Binary message received, length: {length} bytes", result.Count);
                    _ = Task.Run(() => BinaryMessageReceived?.Invoke(this, new ClientBinaryMessageReceivedArgs(buffer[..result.Count], client.Id)), cancellationToken);
                }
                // We have to check if the is shutting down here,
                // because then we already sent the close message and we don't want to send another one
                else if (result.MessageType == WebSocketMessageType.Close && !_serverShuttingDown)
                {
                    _logger?.LogInformation("Received close message from client");
                    _ = Task.Run(() => ClientDisconnected?.Invoke(this, new ClientDisconnectedArgs(result.CloseStatusDescription ?? string.Empty, client.Id)), cancellationToken);
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    break;
                }
            }
        }

        /// <summary>
        /// Handles the event when a passive user expired.
        /// </summary>
        /// <param name="sender">The sender of the event (<see cref="PassiveClients"/>)</param>
        /// <param name="e">The arguments of the event</param>
        private void PassiveClients_ItemExpired(object? sender, ItemExpiredArgs<WebSocketServerClient> e)
        {
            _logger?.LogDebug("Passive client expired: {clientId}", e.Item.Id);

            // Raise the event asynchronously
            // We don't want to block the cleanup process
            Task.Run(() => PassiveUserExpiredEvent?.Invoke(this, new PassiveUserExpiredArgs(e.Item.Id)));
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