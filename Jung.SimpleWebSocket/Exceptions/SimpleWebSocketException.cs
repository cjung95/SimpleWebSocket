// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

namespace Jung.SimpleWebSocket.Exceptions;

/// <summary>
/// Represents the base exception for the SimpleWebSocket project.
/// </summary>
[Serializable]
public class SimpleWebSocketException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleWebSocketException"/> class.
    /// </summary>
    public SimpleWebSocketException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleWebSocketException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public SimpleWebSocketException(string? message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleWebSocketException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
    public SimpleWebSocketException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
