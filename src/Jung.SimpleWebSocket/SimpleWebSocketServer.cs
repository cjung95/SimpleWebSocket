// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket.Contracts;
using Jung.SimpleWebSocket.Delegates;
using Jung.SimpleWebSocket.Exceptions;
using Jung.SimpleWebSocket.Flows;
using Jung.SimpleWebSocket.Models;
using Jung.SimpleWebSocket.Models.EventArguments;
using Jung.SimpleWebSocket.Models.Messages;
using Jung.SimpleWebSocket.Utility;
using Jung.SimpleWebSocket.Wrappers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.WebSockets;

namespace Jung.SimpleWebSocket
{
    /// <summary>
    /// A simple WebSocket server.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="SimpleWebSocketServer"/> class that listens
    /// for incoming connection attempts on the specified local IP address and port number.
    /// </remarks>
    public class SimpleWebSocketServer : IWebSocketServer, IDisposable
    {
        /// <inheritdoc/>
        public IPAddress LocalIpAddress { get; }

        /// <inheritdoc/>
        public int Port { get; }

        /// <summary>
        /// The logger to write internal log messages.
        /// </summary>
        protected readonly ILogger<SimpleWebSocketServer>? _logger;

        /// <summary>
        /// Represents the message dispatcher used to route messages to their appropriate handlers.
        /// </summary>
        private readonly MessageDispatcher _messageDispatcher;

        /// <inheritdoc/>
        public event EventHandler<ClientConnectedArgs>? ClientConnected;
        /// <inheritdoc/>
        public event EventHandler<ClientDisconnectedArgs>? ClientDisconnected;
        /// <inheritdoc/>
        public event EventHandler<ClientMessageReceivedArgs>? MessageReceived;
        /// <inheritdoc/>
        public event EventHandler<ClientBinaryMessageReceivedArgs>? BinaryMessageReceived;
        /// <inheritdoc/>
        public event EventHandler<ClientBinaryMessageSavedArgs>? BinaryMessageSaved;
        /// <inheritdoc/>
        public event AsyncEventHandler<ClientUpgradeRequestReceivedArgs>? ClientUpgradeRequestReceivedAsync;

        /// <summary>
        /// The CancellationTokenSource for managing cancellation.
        /// </summary>
        protected CancellationTokenSource _cancellationTokenSource = new();

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
        /// The options for the server.
        /// </summary>
        private readonly SimpleWebSocketServerOptions _options;

        /// <summary>
        /// A flag indicating whether the server is started.
        /// </summary>
        public bool IsStarted => _isStarted == 1;

        /// <summary>
        /// A flag indicating whether the server is started.
        /// <para>0 = false, 1 = true</para>
        /// </summary>
        private int _isStarted;

        /// <summary>
        /// A flag indicating whether the server is shutting down.
        /// <para>0 = false, 1 = true</para>
        /// </summary>
        private int _serverShuttingDown;

        /// <summary>
        /// A flag indicating whether the server is disposed.
        /// </summary>
        public bool Disposed => _disposed == 1;

        /// <summary>
        /// A flag indicating whether the server is disposed.
        /// <para>0 = false, 1 = true</para>
        /// </summary>
        protected int _disposed;

        /// <summary>
        /// A flag indicating whether the server is disposing.
        /// <para>0 = false, 1 = true</para>
        /// </summary>
        protected int _disposing;

        /// <summary>
        /// The server that listens for incoming connection attempts.
        /// </summary>
        private ITcpListener? _tcpListener;

        /// <summary>
        /// Represents an internal utility for raising asynchronous events.
        /// </summary>
        private readonly AsyncEventRaiser _asyncEventRaiser;

        /// <param name="options">The options for the server</param>
        /// <param name="logger">A logger to write internal log messages</param>
        public SimpleWebSocketServer(SimpleWebSocketServerOptions options, ILogger<SimpleWebSocketServer>? logger = null)
        {
            LocalIpAddress = options.LocalIpAddress;
            Port = options.Port;
            _options = options;
            _logger = logger;

            _messageDispatcher = new MessageDispatcher();
            _asyncEventRaiser = new AsyncEventRaiser(logger);
            RegisterDispatcherHandlers();
        }

        /// <summary>
        /// Registers handlers for various message types with the message dispatcher.
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        private void RegisterDispatcherHandlers()
        {

            _messageDispatcher.RegisterHandler<TextReceivedMessage>((client, message) =>
            {
                var clientId = GetClientId(client);
                _asyncEventRaiser.RaiseAsyncInNewTask(MessageReceived, this, new ClientMessageReceivedArgs(message.ReceivedMessage, clientId), _cancellationTokenSource.Token);
            });

            _messageDispatcher.RegisterHandler<BinaryReceivedMessage>((client, message) =>
            {
                var clientId = GetClientId(client);
                _asyncEventRaiser.RaiseAsyncInNewTask(BinaryMessageReceived, this, new ClientBinaryMessageReceivedArgs(message.ReceivedData, clientId), _cancellationTokenSource.Token);
            });

            _messageDispatcher.RegisterHandler<ConnectionClosedMessage>((client, message) =>
            {
                var id = GetClientId(client);
                _asyncEventRaiser.RaiseAsyncInNewTask(ClientDisconnected, this, new ClientDisconnectedArgs(message.CloseStatusDescription, (WebSocketServerClient)client), _cancellationTokenSource.Token);
            });

            _messageDispatcher.RegisterHandler<BinaryMessageSavedMessage>((client, message) =>
            {
                var id = GetClientId(client);
                _asyncEventRaiser.RaiseAsyncInNewTask(
                           BinaryMessageSaved,
                           this,
                           new ClientBinaryMessageSavedArgs(message.FileStream, message.Length, message.TempFilePath, message.Cleanup, id),
                          _cancellationTokenSource.Token);
            });

            static string GetClientId(SimpleWebSocketBase client)
            {
                return client is WebSocketServerClient serverClient ? serverClient.Id : throw new InvalidOperationException("Client is not a WebSocketServerClient");
            }

        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SimpleWebSocketServer"/> class that listens
        /// for incoming connection attempts on the specified local IP address and port number.
        /// </summary>
        /// <param name="options">The options for the server</param>
        /// <param name="logger">A logger to write internal log messages</param>
        public SimpleWebSocketServer(IOptions<SimpleWebSocketServerOptions> options, ILogger<SimpleWebSocketServer>? logger = null)
            : this(options.Value, logger)
        {
        }

        /// <summary>
        /// Constructor for dependency injection (used in tests)
        /// </summary>
        /// <param name="options">The options for the server</param>
        /// <param name="tcpListener">A wrapped tcp listener</param>
        /// <param name="logger">>A logger to write internal log messages</param>
        internal SimpleWebSocketServer(SimpleWebSocketServerOptions options, ITcpListener tcpListener, ILogger<SimpleWebSocketServer>? logger = null)
            : this(options, logger)
        {
            _tcpListener = tcpListener;
        }

        /// <inheritdoc/>
        public void Start(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (Interlocked.Exchange(ref _isStarted, 1) == 1)
            {
                throw new WebSocketServerException("Server is already started");
            }

            _cancellationTokenSource = new CancellationTokenSource();
            var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cancellationTokenSource.Token);

            _tcpListener ??= new TcpListenerWrapper(LocalIpAddress, Port);
            _tcpListener.Start();
            _ = Task.Run(async delegate
            {
                _logger?.LogInformation("Server started at {LocalIpAddress}:{Port}", LocalIpAddress, Port);
                while (!linkedTokenSource.IsCancellationRequested)
                {
                    try
                    {
                        // Accept the client
                        var tcpClient = await _tcpListener.AcceptTcpClientAsync(linkedTokenSource.Token).ConfigureAwait(false);
                        var client = new WebSocketServerClient(_messageDispatcher, tcpClient, _options, _logger);

                        _logger?.LogDebug("Client connected from {endpoint}", client.ClientConnection!.RemoteEndPoint);

                        _ = HandleClientAsync(client, linkedTokenSource.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        // Ignore the exception, because it is thrown when cancellation is requested
                    }
                    catch (Exception exception)
                    {
                        _logger?.LogError(exception, "Error while accepting Client.");
                    }
                }
            }, linkedTokenSource.Token);
        }

        /// <inheritdoc/>
        public async Task ShutdownServerAsync(WebSocketCloseStatus closeStatus = WebSocketCloseStatus.EndpointUnavailable, string closeDescription = "Server is shutting down", CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (Interlocked.Exchange(ref _serverShuttingDown, 1) == 1)
            {
                _logger?.LogInformation("Server is already shutting down");
                return;
            }

            if (Interlocked.Exchange(ref _isStarted, 0) == 0)
            {
                _logger?.LogInformation("Server is not started");
                return;
            }

            var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cancellationTokenSource.Token);

            _logger?.LogInformation("Shutting down server...");

            // copying the active clients to avoid a collection modified exception
            var activeClients = ActiveClients.Values.ToArray();
            foreach (var client in activeClients)
            {
                try
                {
                    await client.CloseConnectionAsync(closeStatus, closeDescription, linkedTokenSource.Token).ConfigureAwait(false);
                    ActiveClients.TryRemove(client.Id, out _);
                }
                catch
                {
                    // Ignore the exception, because it's not the server's problem if a client does not close the connection
                }
            }

            CleanupServerResources();
            _serverShuttingDown = 0;
            _logger?.LogInformation("Server shutdown complete.");
        }

        /// <inheritdoc/>
        /// <exception cref="WebSocketServerException"></exception>
        public WebSocketServerClient GetClientById(string clientId)
        {
            ThrowIfDisposed();

            if (TryGetClientById(clientId, out var client))
            {
                return client;
            }
            throw new WebSocketServerException(message: "Client not found");
        }

        /// <inheritdoc/>
        public bool TryGetClientById(string clientId, [NotNullWhen(true)] out WebSocketServerClient? client)
        {
            ThrowIfDisposed();

            if (ActiveClients.TryGetValue(clientId, out client))
            {
                if (client != null)
                {
                    return true;
                }
            }
            return false;
        }

        /// <inheritdoc/>
        public void ChangeClientId(WebSocketServerClient client, string newId)
        {
            ThrowIfDisposed();

            // if the client is not found or the new id is already in use, throw an exception
            if (!ActiveClients.TryGetValue(client.Id, out var _)) throw new ClientNotFoundException(message: "A client with the given clientId was not found");
            if (ActiveClients.ContainsKey(newId)) throw new ClientIdAlreadyExistsException(message: "A client with the new clientId already exists");

            // because the id is used as a key in the dictionary,
            // we have to remove the client and add it again with the new id
            if (ActiveClients.TryRemove(client.Id, out _))
            {
                client.UpdateId(newId);
            }
            if (!ActiveClients.TryAdd(newId, client))
            {
                // If adding the client with the new id fails, we have to close the connection and throw an exception
                // otherwise the client would be in an inconsistent state
                _ = client.CloseConnectionAsync(WebSocketCloseStatus.InternalServerError, "Error while changing client clientId", _cancellationTokenSource.Token);
                throw new WebSocketServerException(message: "Error while changing client clientId. The client connection was closed.");
            }
        }

        /// <inheritdoc/>
        public async Task CloseClientConnectionAsync(WebSocketServerClient client, WebSocketCloseStatus closeStatus = WebSocketCloseStatus.NormalClosure, string closeDescription = "Connection closed by server", CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (ActiveClients.TryRemove(client.Id, out var _))
            {
                var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cancellationTokenSource.Token);
                await client.CloseConnectionAsync(closeStatus, closeDescription, linkedTokenSource.Token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Handles the client connection.
        /// </summary>
        /// <param name="client">The client to handle</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>A asynchronous task</returns>
        private async Task HandleClientAsync(WebSocketServerClient client, CancellationToken cancellationToken)
        {
            var flow = new ClientHandlingFlow(this, client, _logger, cancellationToken);
            try
            {
                // Load the request context 
                await flow.LoadRequestContext().ConfigureAwait(false);

                // Raise async client upgrade request received event
                var eventArgs = await flow.RaiseUpgradeEventAsync(ClientUpgradeRequestReceivedAsync).ConfigureAwait(false);

                // Respond to the upgrade request
                if (eventArgs.AcceptRequest)
                {
                    // Accept the WebSocket connection
                    await flow.AcceptWebSocketAsync().ConfigureAwait(false);

                    if (flow.TryAddClientToActiveUserList())
                    {
                        _logger?.LogDebug("Connection upgraded, now listening on Client {clientId}", flow.Client.Id);
                        _asyncEventRaiser.RaiseAsyncInNewTask(ClientConnected, this, new ClientConnectedArgs(flow.Client.Id), cancellationToken);

                        // Start listening for messages
                        await client.ProcessWebSocketMessagesAsync(cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        _logger?.LogDebug("Error while adding Client {clientId} to active clients", flow.Client.Id);
                    }
                }
                else
                {
                    // Reject the WebSocket connection
                    _logger?.LogDebug("Client upgrade request rejected by ClientUpgradeRequestReceivedAsync event.");
                    await flow.RejectWebSocketAsync(eventArgs.ResponseContext).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // Ignore the exception, because it is thrown when cancellation is requested
            }
            catch (Exception exception)
            {
                _logger?.LogError(exception, "Error while handling the Client {clientId}", flow.Client.Id);
            }
            finally
            {
                // Handle disconnected client if the connection was not closed by the server
                if (client.DisconnectOrigin != DisconnectOrigin.Local)
                {
                    flow.HandleDisconnectedClient();
                }
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposing, 1) == 1)
            {
                return;
            }

            try
            {
                // Unsubscribe all event handlers
                ClientConnected = null;
                ClientDisconnected = null;
                MessageReceived = null;
                BinaryMessageReceived = null;
                BinaryMessageSaved = null;
                ClientUpgradeRequestReceivedAsync = null;

                // Shutdown server and free resources
                try
                {
                    ShutdownServerAsync().GetAwaiter().GetResult();
                }
                catch
                {
                    // Ignore exceptions during shutdown
                    CleanupServerResources();
                }

                GC.SuppressFinalize(this);
            }
            finally
            {
                Interlocked.Exchange(ref _disposed, 1);
            }
        }

        private void CleanupServerResources()
        {
            _cancellationTokenSource?.Cancel();
            _tcpListener?.Dispose();
            _tcpListener = null;
            ActiveClients.Clear();
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(Disposed, this);
        }
    }
}