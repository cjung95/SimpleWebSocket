// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket.Delegates;

namespace Jung.SimpleWebSocket.Contracts;

/// <summary>
/// Represents A simple WebSocket client.
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
    /// Gets a value indicating whether the client is connected.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// The user id of the client. If not set, the server did not sent a user id at websocket upgrade.
    /// </summary>
    string? UserId { get; }

    /// <summary>
    /// Event that is raised when a message is received from a client.
    /// </summary>
    event MessageReceivedEventHandler? MessageReceived;

    /// <summary>
    /// Event that is raised when a binary message is received from a client.
    /// </summary>
    event BinaryMessageReceivedEventHandler? BinaryMessageReceived;

    /// <summary>
    /// Event that is raised when a client is disconnected.
    /// </summary>
    event DisconnectedEventHandler? Disconnected;

    /// <summary>
    /// Sends a message to all connected clients asynchronously.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SendMessageAsync(string message, CancellationToken? cancellationToken = null);

    /// <summary>
    /// Starts the WebSocket server asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ConnectAsync(CancellationToken? cancellationToken = null);

    /// <summary>
    /// Stops the WebSocket server asynchronously.
    /// </summary>
    /// <param name="closingStatusDescription">The description why the closing status is initiated.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DisconnectAsync(string closingStatusDescription = "Closing", CancellationToken? cancellationToken = null);
}
