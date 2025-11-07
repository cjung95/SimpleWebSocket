// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

namespace Jung.SimpleWebSocket.Models.Messages
{
    internal class TextReceivedMessage(string receivedMessage) : MessageBase
    {
        public string ReceivedMessage { get; } = receivedMessage;
    }
}
