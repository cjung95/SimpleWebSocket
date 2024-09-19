// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket.Delegates;
using Jung.SimpleWebSocket.Models;
using Jung.SimpleWebSocket.Models.EventArguments;
using System.Net;

namespace Jung.SimpleWebSocket.Contracts;

/// <summary>
/// Represents a WebSocket server.
/// </summary>
public interface IWebSocketServer : IDisposable
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
    /// Gets the client ids of the connected clients.
    /// </summary>
    string[] ClientIds { get; }

    /// <summary>
    /// Gets the number of connected clients.
    /// </summary>
    int ClientCount { get; }

    /// <summary>
    /// Event that is raised when a client is connected.
    /// </summary>
    event ClientConnectedEventHandler? ClientConnected;

    /// <summary>
    /// Event that is raised when a client is disconnected.
    /// </summary>
    event ClientDisconnectedEventHandler ClientDisconnected;

    /// <summary>
    /// Event that is raised when a message is received from a client.
    /// </summary>
    event ClientMessageReceivedEventHandler? MessageReceived;

    /// <summary>
    /// Event that is raised when a binary message is received from a client.
    /// </summary>
    event ClientBinaryMessageReceivedEventHandler? BinaryMessageReceived;

    /// <summary>
    /// Gets a client by its id.
    /// </summary>
    /// <param name="clientId">The id of the client</param>
    /// <returns>The client</returns>
    WebSocketServerClient GetClientById(string clientId);

    /// <summary>
    /// Sends a message to all connected clients asynchronously.
    /// </summary>
    /// <param name="clientId">The client id to send the message to.</param>
    /// <param name="message">The message to send.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SendMessageAsync(string clientId, string message, CancellationToken? cancellationToken = null);

    /// <summary>
    /// Stops the WebSocket server.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ShutdownServer(CancellationToken? cancellationToken = null);

    /// <summary>
    /// Starts the WebSocket server.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    void Start(CancellationToken? cancellationToken = null);
}
