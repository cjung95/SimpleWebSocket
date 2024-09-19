// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

namespace Jung.SimpleWebSocket.Exceptions;

/// <summary>
/// Represents an exception that is thrown when an error occurs in the <see cref="SimpleWebSocketClient"/> class.
/// </summary>
[Serializable]
public class WebSocketClientException : SimpleWebSocketException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketClientException"/> class.
    /// </summary>
    public WebSocketClientException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketClientException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public WebSocketClientException(string? message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketClientException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
    public WebSocketClientException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
