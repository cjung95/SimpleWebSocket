// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket.Delegates;
using Jung.SimpleWebSocket.Exceptions;
using Jung.SimpleWebSocket.Models;
using Jung.SimpleWebSocket.Models.EventArguments;
using System.Diagnostics.CodeAnalysis;
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
    event EventHandler<ClientConnectedArgs>? ClientConnected;

    /// <summary>
    /// Event that is raised when a client is disconnected.
    /// </summary>
    event EventHandler<ClientDisconnectedArgs>? ClientDisconnected;

    /// <summary>
    /// Event that is raised when a message is received from a client.
    /// </summary>
    event EventHandler<ClientMessageReceivedArgs>? MessageReceived;

    /// <summary>
    /// Event that is raised when a binary message is received from a client.
    /// </summary>
    event EventHandler<ClientBinaryMessageReceivedArgs>? BinaryMessageReceived;

    /// <summary>
    /// Async Event that is raised when a client upgrade request is received.
    /// </summary>
    event AsyncEventHandler<ClientUpgradeRequestReceivedArgs>? ClientUpgradeRequestReceivedAsync;

    /// <summary>
    /// Gets a client by its id.
    /// </summary>
    /// <param name="clientId">The id of the client</param>
    /// <returns>The client</returns>
    WebSocketServerClient GetClientById(string clientId);

    /// <summary>
    /// Attempts to get a client by its id.
    /// </summary>
    /// <param name="clientId">The id of the client</param>
    /// <param name="client">The client if found, otherwise null</param>
    /// <returns><see langword="true"/> if the client was found, otherwise <see langword="false"/>.</returns>"
    bool TryGetClientById(string clientId, [NotNullWhen(true)] out WebSocketServerClient? client);

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

    /// <summary>
    /// Changes the id of a client.
    /// </summary>
    /// <param name="client">The client to update</param>
    /// <param name="newId">The new id of the client</param>
    /// <exception cref="ClientNotFoundException">Throws when the client is not found</exception>
    /// <exception cref="ClientIdAlreadyExistsException">Throws when the new id is already in use</exception>
    void ChangeClientId(WebSocketServerClient client, string newId);
}
