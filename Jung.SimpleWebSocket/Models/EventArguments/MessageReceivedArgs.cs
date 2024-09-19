// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

namespace Jung.SimpleWebSocket.Models.EventArguments;

/// <summary>
/// Represents the arguments of the event when a message is received from the server.
/// </summary>
/// <param name="Message">The message that was received.</param>
public record MessageReceivedArgs(string Message);