// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

namespace Jung.SimpleWebSocket.Exceptions
{
    /// <summary>
    /// Represents an exception that occurs during a WebSocket connection attempt.
    /// </summary>
    public class WebSocketConnectionException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WebSocketConnectionException"/> class.
        /// </summary>
        public WebSocketConnectionException() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="WebSocketConnectionException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        public WebSocketConnectionException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="WebSocketConnectionException"/> class with a specified error
        /// message and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception, or <see langword="null"/> if no inner exception is
        /// specified.</param>
        public WebSocketConnectionException(string message, Exception innerException) : base(message, innerException) { }
    }
}
