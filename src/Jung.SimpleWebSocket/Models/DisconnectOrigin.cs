// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

namespace Jung.SimpleWebSocket.Models
{
    /// <summary>
    /// Specifies the origin or reason for a disconnection in a communication session.
    /// </summary>
    /// <remarks>This enumeration provides a set of predefined values that describe the source or cause of a
    /// disconnection. It can be used to determine whether the disconnection was initiated locally, remotely, or due to
    /// an error or system event.</remarks>
    public enum DisconnectOrigin
    {
        /// <summary>
        /// Disconnection was initiated by the local application/this site
        /// </summary>
        Local,

        /// <summary>
        /// Disconnection was initiated by the remote connection partner
        /// </summary>
        Remote,

        /// <summary>
        /// Disconnection due to protocol violation or error
        /// </summary>
        ProtocolError,

        /// <summary>
        /// Disconnection due to application-level error or exception
        /// </summary>
        ApplicationError,

        /// <summary>
        /// Disconnection reason is unknown or unspecified
        /// </summary>
        Unknown
    }
}
