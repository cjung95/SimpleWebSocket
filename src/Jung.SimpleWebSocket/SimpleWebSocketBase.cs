// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket.Contracts;
using Jung.SimpleWebSocket.Exceptions;
using Jung.SimpleWebSocket.MessageProcessors;
using Jung.SimpleWebSocket.Models;
using Jung.SimpleWebSocket.Models.Messages;
using Jung.SimpleWebSocket.Utility;
using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Net.WebSockets;
using System.Text;

namespace Jung.SimpleWebSocket
{
    /// <summary>
    /// Provides a base class for implementing WebSocket communication, including message processing, binary and text
    /// message handling, and resource management.
    /// </summary>
    public abstract class SimpleWebSocketBase : IDisposable
    {
        /// <summary>
        /// The CancellationTokenSource for managing cancellation.
        /// </summary>
        protected CancellationTokenSource _cancellationTokenSource = new();

        /// <summary>
        /// The options for the WebSocket Base.
        /// </summary>
        protected readonly SimpleWebSocketBaseOptions _options;

        /// <summary>
        /// Provides access to the message dispatcher used for sending and processing messages within the system.
        /// </summary>
        private protected readonly IMessageDispatcher _messageDispatcher;

        /// <summary>
        /// Gets or sets the WebSocket of the client.
        /// </summary>
        private protected IWebSocket? WebSocket { get; private set; }

        /// <summary>
        /// A flag indicating whether the base is disposed.
        /// <para>0 = false, 1 = true</para>
        /// </summary>
        protected int _baseDisposed;

        /// <summary>
        /// Represents a factory responsible for creating instances of binary message processors.
        /// </summary>
        private readonly BinaryMessageProcessorFactory _binaryMessageProcessorFactory;

        /// <summary>
        /// The logger to write internal log messages.
        /// </summary>
        protected readonly ILogger? _logger;

        /// <inheritdoc/>
        public DisconnectOrigin DisconnectOrigin { get; protected set; } = DisconnectOrigin.Unknown;

        /// <summary>
        /// Initializes a new instance of the <see cref="SimpleWebSocketBase"/> class.
        /// </summary>
        /// <param name="options">The options for the logic implemented in this base class</param>
        /// <param name="logger">The logger for the simple web socket base</param>
        private protected SimpleWebSocketBase(SimpleWebSocketBaseOptions options, ILogger? logger = null)
            : this(options, new MessageDispatcher(), logger) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="SimpleWebSocketBase"/> class.
        /// </summary>
        /// <param name="options">The options for the logic implemented in this base class</param>
        /// <param name="messageDispatcher">The message dispatcher to use for dispatching messages</param>
        /// <param name="logger">The logger for the simple web socket base</param>
        private protected SimpleWebSocketBase(SimpleWebSocketBaseOptions options, IMessageDispatcher messageDispatcher, ILogger? logger = null)
        {
            _options = options;
            _messageDispatcher = messageDispatcher;
            _logger = logger;
            _binaryMessageProcessorFactory = new BinaryMessageProcessorFactory(options, messageDispatcher);
        }

        /// <summary>
        /// Updates the base with a new WebSocket.
        /// </summary>
        /// <param name="webSocket">The web socket to use.</param>
        internal void UseWebSocket(IWebSocket? webSocket)
        {
            ArgumentNullException.ThrowIfNull(webSocket);
            WebSocket = webSocket;
        }

        private protected async Task SendDataAsync(byte[] data, WebSocketMessageType messageType = WebSocketMessageType.Binary, CancellationToken? cancellationToken = null)
        {
            EnsureWebSocketInitialized();

            cancellationToken ??= CancellationToken.None;
            var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken.Value, _cancellationTokenSource.Token);

            try
            {
                await WebSocket.SendAsync(data.AsMemory(), messageType, true, linkedTokenSource.Token).ConfigureAwait(false);
                _logger?.LogDebug("{MessageType} message sent, length: {length} bytes", Enum.GetName(messageType), data.Length);
            }
            catch (Exception exception)
            {
                _logger?.LogError(exception, "Error sending binary message");
                throw new WebSocketClientException(message: "An Error occurred sending a message.", innerException: exception);
            }
        }

        private protected async Task SendFileAsync(string filePath, int sendFileBufferSize, CancellationToken? cancellationToken = null)
        {
            EnsureWebSocketInitialized();

            cancellationToken ??= CancellationToken.None;
            var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken.Value, _cancellationTokenSource.Token);

            byte[] buffer = ArrayPool<byte>.Shared.Rent(sendFileBufferSize);
            try
            {
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                int bytesRead;
                bool endOfMessage = false;

                while (!endOfMessage)
                {
                    bytesRead = await fileStream.ReadAsync(buffer, linkedTokenSource.Token).ConfigureAwait(false);
                    if (bytesRead == 0)
                    {
                        break;
                    }

                    endOfMessage = fileStream.Position == fileStream.Length;
                    await WebSocket.SendAsync(new Memory<byte>(buffer, 0, bytesRead), WebSocketMessageType.Binary, endOfMessage, linkedTokenSource.Token).ConfigureAwait(false);
                }
                _logger?.LogDebug("File sent: {filePath}", filePath);
            }
            catch (Exception exception)
            {
                _logger?.LogError(exception, "Error while sending a message.");
                throw new WebSocketClientException(message: "Error sending file", innerException: exception);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        /// <summary>
        /// Processes incoming WebSocket messages asynchronously.
        /// </summary>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException">Thrown if the WebSocket is not initialized or if a file stream is unexpectedly null for a binary message.</exception>
        internal async Task ProcessWebSocketMessagesAsync(CancellationToken cancellationToken)
        {
            EnsureWebSocketInitialized();

            string? closeStatusDescription = null;
            var chunkSize = _options.ChunkSize > 0 ? _options.ChunkSize : (4 * 1024);
            var buffer = ArrayPool<byte>.Shared.Rent(chunkSize);
            var maxTextBytes = _options.MaxTextBytes;
            var maxMessageBytes = _options.MaxMessageBytes;
            IBinaryMessageProcessor? binaryMessageProcessor = null;

            try
            {
                while (WebSocket.State == WebSocketState.Open)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    ValueWebSocketReceiveResult result;
                    Decoder? decoder = null;
                    StringBuilder? textBuilder = null;
                    long totalTextBytes = 0;
                    long totalBytes = 0;

                    do
                    {
                        result = await WebSocket.ReceiveAsync(new Memory<byte>(buffer, 0, chunkSize), cancellationToken).ConfigureAwait(false);

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            _logger?.LogDebug("Close message received from remote. Status: {status}, Description: {description}",
                                WebSocket.CloseStatus, WebSocket.CloseStatusDescription);
                            break;
                        }

                        if (result.Count > 0)
                        {
                            totalBytes += result.Count;
                            if (totalBytes > maxMessageBytes)
                            {
                                DisconnectOrigin = DisconnectOrigin.ApplicationError;
                                closeStatusDescription = "Message too large";
                                await WebSocket.CloseAsync(WebSocketCloseStatus.MessageTooBig, closeStatusDescription, CancellationToken.None).ConfigureAwait(false);
                                return;
                            }

                            if (result.MessageType == WebSocketMessageType.Text)
                            {
                                // Until now we only support this way of receiving text messages
                                // so we do not need a message processor for text messages.

                                totalTextBytes += result.Count;
                                if (totalTextBytes > maxTextBytes)
                                {
                                    DisconnectOrigin = DisconnectOrigin.ApplicationError;
                                    closeStatusDescription = "Text message too large";
                                    await WebSocket.CloseAsync(WebSocketCloseStatus.MessageTooBig, closeStatusDescription, CancellationToken.None).ConfigureAwait(false);
                                    return;
                                }

                                decoder ??= Encoding.UTF8.GetDecoder();
                                textBuilder ??= new StringBuilder();

                                var charCount = decoder.GetCharCount(buffer, 0, result.Count);
                                var charBuffer = ArrayPool<char>.Shared.Rent(charCount);
                                try
                                {
                                    decoder.GetChars(buffer, 0, result.Count, charBuffer, 0);
                                    textBuilder.Append(charBuffer, 0, charCount);
                                }
                                finally
                                {
                                    ArrayPool<char>.Shared.Return(charBuffer);
                                }
                            }
                            else if (result.MessageType == WebSocketMessageType.Binary)
                            {
                                binaryMessageProcessor ??= _binaryMessageProcessorFactory.Create();
                                await binaryMessageProcessor.ProcessBinaryMessageChunkAsync(buffer, result.Count, cancellationToken).ConfigureAwait(false);
                            }
                        }
                    }
                    while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        closeStatusDescription = WebSocket.CloseStatusDescription;

                        // If we received a close frame, the remote is initiating the close
                        // so we respond with our own close frame.

                        if (WebSocket.State == WebSocketState.CloseReceived)
                        {
                            DisconnectOrigin = DisconnectOrigin.Remote;
                            var closeStatus = WebSocket.CloseStatus ?? WebSocketCloseStatus.NormalClosure;
                            await WebSocket.CloseAsync(closeStatus, string.Empty, CancellationToken.None).ConfigureAwait(false);
                        }
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Text && textBuilder != null)
                    {
                        var receivedMessage = textBuilder.ToString();
                        _logger?.LogDebug("Message received: {message}", receivedMessage);
                        _messageDispatcher.Dispatch(this, new TextReceivedMessage(receivedMessage));
                    }
                    else if (result.MessageType == WebSocketMessageType.Binary && binaryMessageProcessor != null)
                    {
                        await binaryMessageProcessor.CompleteMessageAsync(this, cancellationToken).ConfigureAwait(false);
                    }

                    // Reset the binary message processor for the next message
                    binaryMessageProcessor = null;
                }
            }
            catch (OperationCanceledException)
            {
                // Operation was canceled, likely due to disposal or external cancellation
            }
            catch (WebSocketException webSocketException)
            {
                // The WebSocket implementation only uses the two WebSocketErrors 
                // WebSocketError.ConnectionClosedPrematurely and WebSocketError.Faulted

                if (webSocketException.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
                {
                    // The connection was closed by the remote host or due to network issues
                    // We consider this as remote disconnection
                    DisconnectOrigin = DisconnectOrigin.Remote;
                    closeStatusDescription = "Connection closed prematurely by the remote host.";
                }
                else if (webSocketException.WebSocketErrorCode == WebSocketError.Faulted)
                {
                    // All other errors are considered protocol errors
                    DisconnectOrigin = DisconnectOrigin.ProtocolError;
                    closeStatusDescription = webSocketException.Message;
                }
                else
                {
                    // This should not happen, but just in case
                    DisconnectOrigin = DisconnectOrigin.Unknown;
                    closeStatusDescription = webSocketException.Message;
                }
                throw;
            }
            catch (Exception exception)
            {
                DisconnectOrigin = DisconnectOrigin.ApplicationError;
                closeStatusDescription = exception.Message;
                throw;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);

                // If the connection was not closed by us, notify about the closed connection
                if (DisconnectOrigin != DisconnectOrigin.Local)
                {
                    _messageDispatcher.Dispatch(this, new ConnectionClosedMessage(closeStatusDescription));
                }
            }
        }

        [MemberNotNull(nameof(WebSocket))]
        private void EnsureWebSocketInitialized()
        {
            if (WebSocket == null) throw new WebSocketClientException(message: "WebSocket is not initialized");
        }

        private protected void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_baseDisposed == 1, this);
        }

        /// <summary>
        /// This is called to dispose the resources used by the SimpleWebSocketBase.
        /// </summary>
        /// <remarks>
        /// This method does not handle any checks for whether the instance is already disposed.
        /// </remarks>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the resources used by the current instance of the class.
        /// </summary>
        /// <param name="disposing"><see langword="true"/> to release both managed and unmanaged resources; <see langword="false"/> to release
        /// only unmanaged resources. This  is the typical pattern for the Dispose method.</param>
        protected virtual void Dispose(bool disposing)
        {
            // Ensure this block runs only once.
            if (Interlocked.Exchange(ref _baseDisposed, 1) == 1)
                return;

            if (disposing)
            {
                try
                {
                    _cancellationTokenSource?.Cancel();
                    WebSocket?.Dispose();
                    WebSocket = null;
                }
                catch
                {
                    // Ignore exceptions during dispose
                }
            }
            // no unmanaged resources to release
        }
    }
}
