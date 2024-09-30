using System.Net;

namespace Jung.SimpleWebSocket.Models
{
    /// <summary>
    /// Represents the options for the SimpleWebSocketServer.
    /// </summary>
    public class SimpleWebSocketServerOptions
    {
        /// <summary>
        /// Gets or sets the local IP address of the server.
        /// </summary>
        public IPAddress LocalIpAddress { get; set; } = IPAddress.Any;

        /// <summary>
        /// Gets or sets the port of the server.
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Switch for remembering disconnected clients.
        /// </summary>
        /// <remarks>
        /// If true the server will put disconnected clients into a passive client list.
        /// This clients can reidentify themselves with their user id.
        /// </remarks>
        public bool RememberDisconnectedClients { get; set; } = false;

        /// <summary>
        /// Switch for removing passive clients after the end of the <see cref="PassiveClientLifetime"/>.
        /// </summary>
        public bool RemovePassiveClientsAfterClientExpirationTime { get; set; } = false;

        /// <summary>
        /// Switch for sending the user id to the client.
        /// </summary>
        public bool SendUserIdToClient { get; set; } = false;

        /// <summary>
        /// The time after which a passive client is removed from the passive client list.
        /// </summary>
        public TimeSpan PassiveClientLifetime { get; set; } = TimeSpan.FromMinutes(1);
    }
}