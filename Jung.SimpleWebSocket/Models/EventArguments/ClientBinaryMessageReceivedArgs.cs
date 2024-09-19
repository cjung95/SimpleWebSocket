// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

namespace Jung.SimpleWebSocket.Models.EventArguments
{
    /// <summary>
    /// Represents the arguments of the event when a message is received from a client.
    /// </summary>
    /// <param name="Message">The message that was received.</param>
    /// <param name="ClientId">The unique identifier of the client that sent the message.</param>
    public record ClientBinaryMessageReceivedArgs(byte[] Message, string ClientId);
}
