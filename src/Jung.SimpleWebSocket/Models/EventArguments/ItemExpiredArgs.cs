// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

namespace Jung.SimpleWebSocket.Models.EventArguments;

/// <summary>
/// Represents the arguments of the event when an item is expired.
/// </summary>
/// <param name="Item">The item that is expired.</param>
public record ItemExpiredArgs<TValue>(TValue Item);