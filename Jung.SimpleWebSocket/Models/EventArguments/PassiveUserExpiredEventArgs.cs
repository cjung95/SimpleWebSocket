// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

namespace Jung.SimpleWebSocket.Models.EventArguments;

/// <summary>
/// Represents the arguments of the event when a passive user expired.
/// </summary>
/// <param name="ClientId">The identifier of the user that expired.</param>
public record PassiveUserExpiredArgs(string ClientId);