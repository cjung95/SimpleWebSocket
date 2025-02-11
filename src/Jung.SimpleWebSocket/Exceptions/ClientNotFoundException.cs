// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

namespace Jung.SimpleWebSocket.Exceptions
{
    /// <summary>
    /// Exception thrown when a client with the given id is not found in the client list.
    /// </summary>
    /// <param name="message">The message to display when the exception is thrown.</param>
    public class ClientNotFoundException(string message) : Exception(message)
    {
    }
}
