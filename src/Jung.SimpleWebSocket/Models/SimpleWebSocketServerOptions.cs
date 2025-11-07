using Jung.SimpleWebSocket.Contracts;
using System.Net;

namespace Jung.SimpleWebSocket.Models
{

    /// <summary>
    /// Options for the SimpleWebSocketServer.
    /// </summary>
    public class SimpleWebSocketServerOptions : SimpleWebSocketBaseOptions
    {

        /// <summary>
        /// The local IP address the server will bind to.
        /// 
        /// Common values:
        /// <list type="bullet">
        /// <item><description><see cref="IPAddress.Any"/> (0.0.0.0) - Listen on all network interfaces</description></item>
        /// <item><description><see cref="IPAddress.Loopback"/> (127.0.0.1) - Listen only on localhost (recommended when behind reverse proxy)</description></item>
        /// <item><description>Specific IP (e.g., 192.168.1.100) - Listen on specific interface only</description></item>
        /// </list>
        /// 
        /// Default: <see cref="IPAddress.Any"/> - allows the server to accept connections from any network interface when running as a standalone service.
        /// </summary>
        public IPAddress LocalIpAddress { get; set; } = IPAddress.Any;

        /// <summary>
        /// Gets or sets the port number on which the server will listen for incoming connections.
        /// 
        /// Considerations:
        /// <list type="bullet">
        /// <item><description>Well-known ports (0-1023): Typically require administrator privileges</description></item>
        /// <item><description>Registered ports (1024-49151): Common for applications</description></item>
        /// <item><description>Dynamic/private ports (49152-65535): Available for temporary use</description></item>
        /// </list>
        /// 
        /// When choosing a port:
        /// <list type="bullet">
        /// <item><description>Avoid ports used by common services (80, 443, 22, 21, 25, etc.)</description></item>
        /// <item><description>Check firewall rules to ensure the port is accessible</description></item>
        /// <item><description>Consider using ports above 1024 to avoid requiring elevated privileges</description></item>
        /// </list>
        /// 
        /// Default: 8080 - A common alternative HTTP port that typically doesn't require admin privileges.
        /// </summary>
        public int Port { get; set; } = 8080;

       
    }
}