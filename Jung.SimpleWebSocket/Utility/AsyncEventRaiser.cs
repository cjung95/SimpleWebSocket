// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket.Delegates;

namespace Jung.SimpleWebSocket.Utility
{
    /// <summary>
    /// Helper class to raise an async event.
    /// </summary>
    internal class AsyncEventRaiser
    {
        /// <summary>
        /// Helper method to raise an async event.
        /// </summary>
        /// <typeparam name="TEventArgs">The type of the event arguments.</typeparam>
        /// <param name="asyncEvent">The async event handler</param>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="e">The event arguments.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        internal static async Task RaiseAsync<TEventArgs>(AsyncEventHandler<TEventArgs>? asyncEvent, object sender, TEventArgs e, CancellationToken cancellationToken) where TEventArgs : class
        {
            var syncContext = SynchronizationContext.Current; // Capture the current synchronization context

            if (asyncEvent != null)
            {
                var invocationList = asyncEvent.GetInvocationList();

                foreach (var handler in invocationList)
                {
                    var asyncHandler = (AsyncEventHandler<TEventArgs>)handler;

                    if (syncContext != null)
                    {
                        // Post back to the captured context if it's not null
                        syncContext.Post(async _ =>
                        {
                            await asyncHandler(sender, e, cancellationToken);
                        }, null);
                    }
                    else
                    {
                        // Execute directly if there's no synchronization context
                        await asyncHandler(sender, e, cancellationToken);
                    }
                }
            }
        }
    }
}
