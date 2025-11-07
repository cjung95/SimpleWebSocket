// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using System.Net;

namespace Jung.SimpleWebSocket.Models
{
    /// <summary>
    /// Represents the configuration options for a simple WebSocket client.
    /// </summary>
    public class SimpleWebSocketClientOptions : SimpleWebSocketBaseOptions
    {
        /// <summary>
        /// The host name of the WebSocket server to connect to. If not set, <see cref="IPAddress"/> will be used.
        /// </summary>
        public string? HostName { get; set; }

        /// <summary>
        /// The IP address of the WebSocket server to connect to. If not set, <see cref="HostName"/> will be used.
        /// </summary>
        public IPAddress? IPAddress { get; set; }

        /// <summary>
        /// TCP port to connect to. Defaults to 8080.
        /// </summary>
        public int Port { get; set; } = 8080;

        /// <summary>
        /// Request path portion of the WebSocket endpoint. Defaults to "/".
        /// </summary>
        public string RequestPath { get; set; } = "/";

        /// <summary>
        /// Returns the effective host as a string, preferring HostName over IPAddress.
        /// </summary>
        public string Host => HostName ?? IPAddress?.ToString() ?? throw new InvalidOperationException("Either HostName or IPAddress must be set.");
    }
}