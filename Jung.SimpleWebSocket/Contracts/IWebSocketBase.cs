namespace Jung.SimpleWebSocket.Contracts
{
    /// <summary>
    /// Represents a base for a WebSocket.
    /// </summary>
    public interface IWebSocketBase
    {
        /// <summary>
        /// Event that is raised when a message is received from a client.
        /// </summary>
        event Action<string>? MessageReceived;

        /// <summary>
        /// Event that is raised when a binary message is received from a client.
        /// </summary>
        event Action<byte[]>? BinaryMessageReceived;

        /// <summary>
        /// Event that is raised when a client is disconnected.
        /// </summary>
        event Action<object?>? ClientDisconnected;
    }
}
