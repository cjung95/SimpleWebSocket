// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

namespace Jung.SimpleWebSocket.Contracts;

/// <summary>
/// Represents a WebSocket server.
/// </summary>
public interface IWebSocketClient : IDisposable
{
    /// <summary>
    /// Gets the local ip address of the WebSocket server.
    /// </summary>
    string HostName { get; }

    /// <summary>
    /// Gets the port of the WebSocket server.
    /// </summary>
    int Port { get; }

    /// <summary>
    /// Gets the request path of the WebSocket server.
    /// </summary>
    string RequestPath { get; }

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
    /// Starts the WebSocket server asynchronously.
    /// </summary>
    /// <param name="cancellation">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ConnectAsync(CancellationToken cancellation);

    /// <summary>
    /// Stops the WebSocket server asynchronously.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DisconnectAsync();
}
