// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket.Contracts;
using Jung.SimpleWebSocket.Models;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.WebSockets;
using System.Text;

namespace Jung.SimpleWebSocket
{
    /// <summary>
    /// Represents a WebSocket client of the server.
    /// </summary>
    public class WebSocketServerClient : SimpleWebSocketBase
    {
        /// <summary>
        /// Gets the unique identifier of the WebSocket client.
        /// </summary>
        public string Id { get; private set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Gets the properties of the WebSocket client.
        /// </summary>
        /// <remarks>
        /// The properties can be used to store additional information about the client.
        /// </remarks>
        public Dictionary<string, object> Properties { get; } = [];

        /// <summary>
        /// Gets the connection of the WebSocket client.
        /// </summary>
        internal ITcpClient? ClientConnection { get; private set; }

        /// <summary>
        /// Gets the timestamp when the WebSocket client was last seen.
        /// </summary>
        public DateTime LastConnectionTimestamp { get; private set; }

        /// <summary>
        /// Gets the timestamp when the WebSocket client was first seen.
        /// </summary>
        public DateTime FirstSeen { get; private set; }

        /// <summary>
        /// Gets the remote endpoint of the WebSocket client.
        /// </summary>
        public EndPoint? RemoteEndPoint => ClientConnection?.RemoteEndPoint;

        /// <summary>
        /// A flag indicating whether the sever connection is disposed.
        /// </summary>
        private int _disposed = 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="WebSocketServerClient"/> class.
        /// </summary>
        /// <param name="dispatcher"></param>
        /// <param name="clientConnection">The connection of the client.</param>
        /// <param name="logger"></param>
        /// <param name="options"></param>
        internal WebSocketServerClient(IMessageDispatcher dispatcher, ITcpClient clientConnection, SimpleWebSocketBaseOptions options, ILogger<SimpleWebSocketServer>? logger)
            : base(options, dispatcher, logger)
        {
            ClientConnection = clientConnection;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WebSocketServerClient"/> class without a client connection.
        /// </summary>
        /// <remarks>
        /// This constructor is used for unit tests
        /// </remarks>
        internal WebSocketServerClient()
            : base(new SimpleWebSocketServerOptions())
        {
            // Set timestamps to now
            FirstSeen = DateTime.UtcNow;
            LastConnectionTimestamp = FirstSeen;

        }

        /// <summary>
        /// Updates the WebSocket client with a new identifier.
        /// </summary>
        /// <param name="id">The new identifier of the client</param>
        /// <exception cref="ArgumentException">Throws when the id is not a valid <see cref="Guid"/> or <see cref="Guid.Empty"/>.</exception>
        internal void UpdateId(string id)
        {
            if (!Guid.TryParse(id, out var guid))
            {
                throw new ArgumentException("Id is not a valid Guid", nameof(id));
            }

            if (guid == Guid.Empty)
            {
                throw new ArgumentException("Id cannot be empty", nameof(id));
            }

            Id = id;
        }

        /// <inheritdoc/>
        public async Task SendMessageAsync(string message, CancellationToken? cancellationToken = null)
        {
            ThrowIfDisposed();
            var buffer = Encoding.UTF8.GetBytes(message);
            await SendDataAsync(buffer, WebSocketMessageType.Text, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task SendBinaryDataAsync(byte[] data, CancellationToken? cancellationToken = null)
        {
            ThrowIfDisposed();
            await SendDataAsync(data, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task SendFileAsync(string filePath, CancellationToken? cancellationToken = null)
        {
            ThrowIfDisposed();
            if (!File.Exists(filePath)) throw new FileNotFoundException("File not found", filePath);
            await SendFileAsync(filePath, _options.SendFileBufferSize, cancellationToken).ConfigureAwait(false);
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
                    ClientConnection?.Dispose();
                    ClientConnection = null;
                }
                catch
                {
                    // Ignore exceptions during disposal
                }
            }
            // no unmanaged resources to release here

            base.Dispose(disposing);
        }

        internal async Task CloseConnectionAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
        {
            DisconnectOrigin = DisconnectOrigin.Local;
            if (WebSocket != null && WebSocket.State == WebSocketState.Open)
            {
                await WebSocket.CloseAsync(closeStatus, statusDescription, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}