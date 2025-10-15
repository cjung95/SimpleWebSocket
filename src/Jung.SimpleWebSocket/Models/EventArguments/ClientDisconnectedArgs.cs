// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

namespace Jung.SimpleWebSocket.Models.EventArguments;

/// <summary>  
/// Represents the arguments of the event when a client disconnects from the server.  
/// </summary>  
/// <param name="ClosingStatusDescription">The reason for the connection closure. <see langword="null"/> if the remote party closed the WebSocket connection without completing the close handshake.</param>
/// <param name="Client">The Client that disconnected from the server.</param>
public record ClientDisconnectedArgs(
    string? ClosingStatusDescription,
    // We use the client object here instead of just the client ID to give more context about the disconnected client.
    // When the event is fired, the client is already removed from the active clients list, so we can't access it there.
    WebSocketServerClient Client);
