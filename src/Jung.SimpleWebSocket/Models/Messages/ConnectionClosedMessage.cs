// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

namespace Jung.SimpleWebSocket.Models.Messages
{
    internal class ConnectionClosedMessage(string? closeStatusDescription) : MessageBase
    {
        public string? CloseStatusDescription { get; } = closeStatusDescription;
    }
}
