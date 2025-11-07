// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket.Delegates;
using Jung.SimpleWebSocket.Exceptions;
using Jung.SimpleWebSocket.Models.EventArguments;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.WebSockets;

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
    /// Event raised when a binary message was streamed in-memory.
    /// </summary>
    event EventHandler<ClientBinaryMessageReceivedArgs>? BinaryMessageReceived;

    /// <summary>
    /// Event raised when a binary message was streamed to disk.
    /// </summary>
    event EventHandler<ClientBinaryMessageSavedArgs>? BinaryMessageSaved;

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
    /// Starts the WebSocket server.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    void Start(CancellationToken cancellationToken = default);

    /// <summary>
    /// Shuts down the server and sends close frames to all connected clients with the specified close status and description.
    /// </summary>
    /// <remarks>
    /// The server can be restarted by calling the <see cref="Start(CancellationToken)"/> method again.
    /// </remarks>
    /// <param name="closeStatus">The WebSocket close status.</param>
    /// <param name="closeDescription">The close description.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ShutdownServerAsync(WebSocketCloseStatus closeStatus = WebSocketCloseStatus.EndpointUnavailable, string closeDescription = "Server is shutting down", CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes the id of a client. If the new id is already in use, an exception is thrown.
    /// </summary>
    /// <param name="client">The client to update</param>
    /// <param name="newId">The new id of the client</param>
    /// <exception cref="ClientNotFoundException">Throws when the client is not found</exception>
    /// <exception cref="ClientIdAlreadyExistsException">Throws when the new id is already in use</exception>
    void ChangeClientId(WebSocketServerClient client, string newId);

    /// <summary>
    /// Closes the connection to the specified WebSocket client.
    /// </summary>
    /// <remarks>This method removes the specified client from the active clients list and initiates
    /// an asynchronous operation to close the connection. If the client is not found in the active clients list, no
    /// action is taken.</remarks>
    /// <param name="client">The WebSocket client whose connection is to be closed. Cannot be null.</param>
    /// <param name="closeStatus">The status code indicating the reason for closing the connection. Defaults to <see
    /// cref="WebSocketCloseStatus.NormalClosure"/>.</param>
    /// <param name="closeDescription">A description of the reason for closing the connection. Defaults to "Connection closed by server".</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests. Defaults to <see cref="CancellationToken.None"/>.</param>
    Task CloseClientConnectionAsync(WebSocketServerClient client, WebSocketCloseStatus closeStatus = WebSocketCloseStatus.NormalClosure, string closeDescription = "Connection closed by server", CancellationToken cancellationToken = default);
}
