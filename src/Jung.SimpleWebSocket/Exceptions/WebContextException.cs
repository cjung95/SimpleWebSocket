// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket.Models;

namespace Jung.SimpleWebSocket.Exceptions;

/// <summary>
/// Represents an exception that is thrown when an error occurs in the <see cref="WebContext"/> class.
/// </summary>
[Serializable]
public class WebContextException : SimpleWebSocketException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WebContextException"/> class.
    /// </summary>
    public WebContextException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebContextException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public WebContextException(string? message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebContextException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
    public WebContextException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
