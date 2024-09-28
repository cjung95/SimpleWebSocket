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
        /// Gets or sets the state of the user handling.
        /// </summary>
        public bool ActivateUserHandling { get; set; } = false;

        /// <summary>
        /// The time after which a passive client is removed from the passive client list.
        /// </summary>
        public TimeSpan PassiveClientLifetime { get; set; } = TimeSpan.FromMinutes(1);
    }
}
