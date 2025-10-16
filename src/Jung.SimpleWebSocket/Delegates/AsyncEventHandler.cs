// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

namespace Jung.SimpleWebSocket.Delegates;

/// <summary>
/// Represents an asynchronous event handler.
/// </summary>
/// <typeparam name="TEventArgs">The type of the event arguments.</typeparam>
/// <param name="sender">The sender of the event.</param>
/// <param name="e">The event arguments.</param>
/// <param name="cancellationToken">The cancellation token.</param>
/// <returns>A task that represents the asynchronous operation.</returns>
public delegate Task AsyncEventHandler<TEventArgs>(object sender, TEventArgs e, CancellationToken cancellationToken) where TEventArgs : class;