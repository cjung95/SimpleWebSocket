// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

namespace Jung.SimpleWebSocket.Models.Messages
{
    internal class BinaryReceivedMessage(byte[] receivedData) : MessageBase
    {
        public byte[] ReceivedData { get; } = receivedData;
    }
}
