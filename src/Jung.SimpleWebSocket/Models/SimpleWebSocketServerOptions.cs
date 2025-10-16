// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

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
        /// Gets or sets the log level of the server.
        /// </summary>
        public string LogLevel { get; set; } = "Information";
    }
}
