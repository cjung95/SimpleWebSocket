// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket.Models.EventArguments;

namespace Jung.SimpleWebSocket.Delegates;

/// <summary>
/// The event handler for the disconnected event.
/// </summary>
/// <param name="sender">The sender of the event.</param>
/// <param name="e">The arguments of the event.</param>
public delegate void DisconnectedEventHandler(object sender, DisconnectedArgs e);