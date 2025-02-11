// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

namespace Jung.SimpleWebSocket.Models.EventArguments;

/// <summary>
/// Represents the arguments of the event when the client disconnects from the server.
/// </summary>
/// <param name="ClosingStatusDescription">The description why the closing status was initiated.</param>
public record DisconnectedArgs(string ClosingStatusDescription);