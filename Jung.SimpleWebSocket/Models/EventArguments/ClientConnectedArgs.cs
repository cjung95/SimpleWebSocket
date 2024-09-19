// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

namespace Jung.SimpleWebSocket.Models.EventArguments;

/// <summary>
/// Represents the arguments of the event when a client connects to the server.
/// </summary>
/// <param name="ClientId">The unique identifier of the client that connected to the server.</param>
public record ClientConnectedArgs(string ClientId);