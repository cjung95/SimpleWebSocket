// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

namespace Jung.SimpleWebSocket.Models.EventArguments;

/// <summary>
/// Represents the arguments of the event when a client disconnects from the server.
/// </summary>
/// <param name="ClosingStatusDescription">The description why the closing status was initiated.</param>
/// <param name="ClientId">The unique identifier of the client that disconnected from the server.</param>
public record ClientDisconnectedArgs(string ClosingStatusDescription, string ClientId);