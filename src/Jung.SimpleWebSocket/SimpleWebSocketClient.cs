// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket.Contracts;
using Jung.SimpleWebSocket.Delegates;
using Jung.SimpleWebSocket.Exceptions;
using Jung.SimpleWebSocket.Models;
using Jung.SimpleWebSocket.Models.EventArguments;
using Jung.SimpleWebSocket.Models.Messages;
using Jung.SimpleWebSocket.Utility;
using Jung.SimpleWebSocket.Wrappers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Sockets;
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
    public class SimpleWebSocketClient : SimpleWebSocketBase, IWebSocketClient
    {
        /// <inheritdoc/>
        public string Host { get; }
        /// <inheritdoc/>
        public int Port { get; }
        /// <inheritdoc/>
        public string RequestPath { get; }

        /// <inheritdoc/>
        public bool IsConnected => _client?.Connected ?? false;

        /// <inheritdoc/>
        public event EventHandler<DisconnectedArgs>? Disconnected;
        /// <inheritdoc/>
        public event EventHandler<MessageReceivedArgs>? MessageReceived;
        /// <inheritdoc/>
        public event EventHandler<BinaryMessageReceivedArgs>? BinaryMessageReceived;
        /// <inheritdoc/>
        public event EventHandler<BinaryMessageSavedArgs>? BinaryMessageSaved;

        /// <inheritdoc/>
        public event AsyncEventHandler<SendingUpgradeRequestArgs>? SendingUpgradeRequestAsync;

        /// <summary>
        /// The client that is used to connect to the server.
        /// </summary>
        private TcpClientWrapper? _client;

        /// <summary>
        /// The stream that is used to communicate with the server.
        /// </summary>
        private INetworkStream? _stream;

        /// <summary>
        /// A value indicating whether the client is disconnecting.
        /// <para>0=Not disconnecting, 1=Disconnecting</para>
        /// </summary>
        private int _clientIsDisconnecting = 0;

        /// <summary>
        /// A flag indicating whether the sever connection is disposed.
        /// <para>0=Not Disposed, 1=Disposed</para>
        /// </summary>
        private int _disposed = 0;

        /// <summary>
        /// Represents an internal utility for raising asynchronous events.
        /// </summary>
        private readonly AsyncEventRaiser _asyncEventRaiser;

        /// <summary>
        /// Initializes a new instance of the <see cref="SimpleWebSocketClient"/> class with the specified options and an optional logger.
        /// </summary>
        /// <param name="options">The configuration options for the WebSocket client</param>
        /// <param name="logger">A logger to write internal log messages</param>
        public SimpleWebSocketClient(SimpleWebSocketClientOptions options, ILogger<SimpleWebSocketClient>? logger = null)
            : base(options, logger)
        {
            Host = options.Host;
            Port = options.Port;
            RequestPath = options.RequestPath;

            _asyncEventRaiser = new AsyncEventRaiser(logger);

            InitializeMessageDispatcher();
        }

        /// <summary>
        /// registers message handlers to the dispatcher
        /// </summary>
        private void InitializeMessageDispatcher()
        {
            _messageDispatcher.RegisterHandler<BinaryMessageSavedMessage>((_, message) =>
                _asyncEventRaiser.RaiseAsyncInNewTask(BinaryMessageSaved, this, new BinaryMessageSavedArgs(message.FileStream, message.Length, message.TempFilePath, message.Cleanup), _cancellationTokenSource.Token));

            _messageDispatcher.RegisterHandler<BinaryReceivedMessage>((_, message) =>
                _asyncEventRaiser.RaiseAsyncInNewTask(BinaryMessageReceived, this, new BinaryMessageReceivedArgs(message.ReceivedData), _cancellationTokenSource.Token));

            _messageDispatcher.RegisterHandler<ConnectionClosedMessage>((_, message) =>
            {
                _asyncEventRaiser.RaiseAsyncInNewTask(Disconnected, this, new DisconnectedArgs(message.CloseStatusDescription ?? string.Empty), _cancellationTokenSource.Token);
            });

            _messageDispatcher.RegisterHandler<TextReceivedMessage>((_, message) =>
                _asyncEventRaiser.RaiseAsyncInNewTask(MessageReceived, this, new MessageReceivedArgs(message.ReceivedMessage), _cancellationTokenSource.Token));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SimpleWebSocketClient"/> class with the specified options and
        /// an optional logger.
        /// </summary>
        /// <param name="options">The configuration options for the WebSocket client. Cannot be null.</param>
        /// <param name="logger">An optional logger for capturing log messages. If null, logging is disabled.</param>
        public SimpleWebSocketClient(IOptions<SimpleWebSocketClientOptions> options, ILogger<SimpleWebSocketClient>? logger = null)
            : this(options.Value, logger)
        {
        }

        /// <inheritdoc/>
        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            // Reset the disconnect origin
            DisconnectOrigin = DisconnectOrigin.Unknown;

            if (IsConnected) throw new WebSocketClientException(message: "Client is already connected");

            _cancellationTokenSource = new CancellationTokenSource();
            using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cancellationTokenSource.Token);

            _logger?.LogInformation("Connecting to Server");
            try
            {
                _client = new TcpClientWrapper();
                await _client.ConnectAsync(Host, Port).ConfigureAwait(false);
                await HandleWebSocketInitiation(_client, linkedTokenSource.Token).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _logger?.LogError(exception, "Error connecting to Server");
                if (exception is SocketException)
                {
                    throw new WebSocketConnectionException(message: "Error connecting to Server", innerException: exception);
                }
                else if (exception is WebSocketException || exception is WebSocketUpgradeException)
                {
                    throw;
                }
                else
                {
                    throw new WebSocketClientException(message: "Error connecting to Server", innerException: exception);
                }
            }

            _logger?.LogDebug("Connection upgraded, now listening.");
            _ = RunAndMonitorListenerBackgroundTask();
        }

        /// <summary>
        /// Executes the WebSocket message processing task and monitors for errors during execution.
        /// </summary>
        /// <remarks>This method runs the WebSocket message processing asynchronously and logs any
        /// exceptions that occur during execution. If an error is encountered, the cancellation token is triggered to
        /// signal termination of the operation.</remarks>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task RunAndMonitorListenerBackgroundTask()
        {
            try
            {
                // We are using the internal cancellation token instead of the linked token
                // because the passed cancellation token in the ConnectAsync method is intended for the connection process 
                // and not for this background task.
                await ProcessWebSocketMessagesAsync(_cancellationTokenSource.Token).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _logger?.LogError(exception, "An error occurred while listening to WebSocket messages.");
            }
            finally
            {
                CancelAndDisposeResources();
            }
        }

        /// <inheritdoc/>
        public async Task DisconnectAsync(string closingStatusDescription = "Closing", CancellationToken cancellationToken = default)
        {
            // Make sure we only disconnect once
            if (Interlocked.Exchange(ref _clientIsDisconnecting, 1) == 1)
            {
                return;
            }

            var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cancellationTokenSource.Token);


            if (WebSocket != null && (WebSocket.State == WebSocketState.Open || WebSocket.State == WebSocketState.CloseReceived))
            {
                try
                {
                    _logger?.LogInformation("Disconnecting from Server");
                    DisconnectOrigin = DisconnectOrigin.Local;
                    await WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, closingStatusDescription, linkedTokenSource.Token).ConfigureAwait(false);
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
            _clientIsDisconnecting = 0;
        }

        /// <summary>
        /// Handles the WebSocket initiation.
        /// </summary>
        /// <param name="client">The client to use for the WebSocket initiation</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task HandleWebSocketInitiation(TcpClientWrapper client, CancellationToken cancellationToken)
        {
            // Upgrade the connection to a WebSocket
            _stream = client.GetStream();
            var socketWrapper = new WebSocketUpgradeHandler(_stream);

            var requestContext = WebContext.CreateRequest(Host, Port, RequestPath);
            requestContext = await RaiseUpgradeEventAsync(requestContext, cancellationToken).ConfigureAwait(false);
            await socketWrapper.SendUpgradeRequestAsync(requestContext, cancellationToken).ConfigureAwait(false);
            var response = await socketWrapper.AwaitContextAsync(cancellationToken).ConfigureAwait(false);
            WebSocketUpgradeHandler.ValidateUpgradeResponse(response, requestContext);

            var webSocket = socketWrapper.CreateWebSocket(isServer: false);
            UseWebSocket(webSocket);
        }

        /// <summary>
        /// Raises the upgrade event.
        /// </summary>
        /// <param name="requestContext">The request context to use for the upgrade event</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The event arguments of the upgrade request.</returns>
        internal async Task<WebContext> RaiseUpgradeEventAsync(WebContext requestContext, CancellationToken cancellationToken)
        {
            var eventArgs = new SendingUpgradeRequestArgs(requestContext, _logger);
            await _asyncEventRaiser.RaiseAsync(SendingUpgradeRequestAsync, this, eventArgs, cancellationToken).ConfigureAwait(false);
            return requestContext;
        }

        /// <inheritdoc/>
        public async Task SendTextMessageAsync(string message, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            EnsureConnected();

            var buffer = Encoding.UTF8.GetBytes(message);
            await SendDataAsync(buffer, WebSocketMessageType.Text, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task SendBinaryDataAsync(byte[] data, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            EnsureConnected();

            await SendDataAsync(data, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task SendFileAsync(string filePath, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            EnsureConnected();

            if (!File.Exists(filePath)) throw new FileNotFoundException("File not found", filePath);

            await SendFileAsync(filePath, _options.SendFileBufferSize, cancellationToken).ConfigureAwait(false);
        }

        private void EnsureConnected()
        {
            if (!IsConnected) throw new WebSocketClientException(message: "Client is not connected");
        }

        /// <summary>
        /// Releases the resources used by the current instance of the class.
        /// </summary>
        /// <param name="disposing"><see langword="true"/> to release both managed and unmanaged resources; <see langword="false"/> to release
        /// only unmanaged resources. This  is the typical pattern for the Dispose method.</param>
        protected override void Dispose(bool disposing)
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return;
            }

            if (disposing)
            {

                try
                {
                    // Unsubscribe all event handlers
                    Disconnected = null;
                    MessageReceived = null;
                    BinaryMessageReceived = null;
                    SendingUpgradeRequestAsync = null;

                    CancelAndDisposeResources();
                }
                catch
                {
                    // Ignore exceptions during disposal
                }
            }
            // no unmanaged resources to release here

            base.Dispose(disposing);
        }

        private void CancelAndDisposeResources()
        {
            _cancellationTokenSource?.Cancel();
            _stream?.Dispose();
            _stream = null;
            _client?.Dispose();
            _client = null;
        }
    }
}