// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using System.Net;

namespace Jung.SimpleWebSocket.Contracts;

/// <summary>
/// Represents a WebSocket server.
/// </summary>
public interface IWebSocketServer : IWebSocketBase, IDisposable
{
    /// <summary>
    /// Gets the local ip address of the WebSocket server.
    /// </summary>
    IPAddress LocalIpAddress { get; }

    /// <summary>
    /// Gets the port of the WebSocket server.
    /// </summary>
    int Port { get; }

    /// <summary>
    /// Gets a value indicating whether the server is listening.
    /// </summary>
    bool IsListening { get; }

    /// <summary>
    /// Event that is raised when a client is connected.
    /// </summary>
    event Action<object?>? ClientConnected;

    /// <summary>
    /// Sends a message to all connected clients asynchronously.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SendMessageAsync(string message, CancellationToken cancellationToken);

    /// <summary>
    /// Starts the WebSocket server.
    /// </summary>
    /// <param name="cancellation">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    void Start(CancellationToken cancellation);
}
