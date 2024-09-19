// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket.Contracts;
using System.Net;

namespace Jung.SimpleWebSocket.Models
{
    /// <summary>
    /// Represents a WebSocket client of the server.
    /// </summary>
    public class WebSocketServerClient
    {
        /// <summary>
        /// Gets the unique identifier of the WebSocket client.
        /// </summary>
        public string Id { get; private set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Gets the connection of the WebSocket client.
        /// </summary>
        internal ITcpClient ClientConnection { get; private set; }

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
        public EndPoint? RemoteEndPoint => ClientConnection.RemoteEndPoint;

        /// <summary>
        /// Gets or sets the WebSocket of the client.
        /// </summary>
        internal IWebSocket? WebSocket { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="WebSocketServerClient"/> class.
        /// </summary>
        /// <param name="clientConnection">The connection of the client.</param>
        internal WebSocketServerClient(ITcpClient clientConnection)
        {
            FirstSeen = DateTime.UtcNow;
            LastConnectionTimestamp = FirstSeen;
            ClientConnection = clientConnection;
        }

        /// <summary>
        /// Updates the WebSocket client with a new connection.
        /// </summary>
        /// <param name="client">The new connection of the client.</param>
        internal void UpdateClient(ITcpClient client)
        {
            LastConnectionTimestamp = DateTime.UtcNow;
            ClientConnection = client;
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

        internal void UpdateWebSocket(IWebSocket? webSocket)
        {
            ArgumentNullException.ThrowIfNull(webSocket);
            WebSocket = webSocket;
        }
    }
}