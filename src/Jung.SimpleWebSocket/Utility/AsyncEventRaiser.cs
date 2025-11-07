// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket.Delegates;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace Jung.SimpleWebSocket.Utility
{
    /// <summary>
    /// Helper class to raise an async event.
    /// </summary>
    internal class AsyncEventRaiser(ILogger? logger)
    {
        private readonly ILogger? _logger = logger;

        /// <summary>
        /// Helper method to raise an async event.
        /// </summary>
        /// <typeparam name="TEventArgs">The type of the event arguments.</typeparam>
        /// <param name="asyncEvent">The async event handler.</param>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="e">The event arguments.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <param name="eventName">Name of the parameter <paramref name="asyncEvent"/>. Supplied automatically by the compiler</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        internal async Task RaiseAsync<TEventArgs>(AsyncEventHandler<TEventArgs>? asyncEvent, object sender, TEventArgs e, CancellationToken cancellationToken, [CallerArgumentExpression(nameof(asyncEvent))] string? eventName = null) where TEventArgs : class
        {
            if (asyncEvent == null) return;

            var syncContext = SynchronizationContext.Current;
            var invocationList = asyncEvent.GetInvocationList();
            _logger?.LogDebug("Raise async event \"{EventName}\".", eventName);

            foreach (var handler in invocationList)
            {
                var asyncHandler = (AsyncEventHandler<TEventArgs>)handler;

                try
                {
                    // Currently we do not have the case that we need to post to a synchronization context.
                    // But in case we need it in the future, the code is here.
                    if (syncContext != null)
                    {
                        // Post to captured context but await the handler via a TaskCompletionSource
                        var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

                        syncContext.Post(state =>
                        {
                            try
                            {
                                var task = asyncHandler(sender, e, cancellationToken);
                                task.ContinueWith(t =>
                                {
                                    if (t.IsFaulted)
                                        tcs.TrySetException(t.Exception!.InnerExceptions);
                                    else if (t.IsCanceled)
                                        tcs.TrySetCanceled();
                                    else
                                        tcs.TrySetResult(null);
                                }, TaskScheduler.Default);
                            }
                            catch (Exception ex)
                            {
                                tcs.TrySetException(ex);
                            }
                        }, null);

                        await tcs.Task.ConfigureAwait(false);
                    }
                    else
                    {
                        // No synchronization context, just await the handler
                        await asyncHandler(sender, e, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error in \"{EventName}\" async event handler.", eventName);
                }
            }
        }

        /// <summary>
        /// Helper method to raise an Event in a new Task.
        /// </summary>
        /// <typeparam name="TEventArgs">The type of the event arguments.</typeparam>
        /// <param name="event">The event handler</param>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="e">The event arguments.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <param name="eventName">Name of the parameter <paramref name="event"/>. Supplied automatically by the compiler</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        internal void RaiseAsyncInNewTask<TEventArgs>(EventHandler<TEventArgs>? @event, object sender, TEventArgs e, CancellationToken cancellationToken, [CallerArgumentExpression(nameof(@event))] string? eventName = null) where TEventArgs : class
        {
            if (@event == null) return;

            // We check the cancellation token here and do not pass it to the Task, because the task starts delayed 
            // if we are not debugging it (yes - it is handled differently while debugging, i had to learn this the hard way).
            // In this case the task should run even if the CT is cancelled.
            // Otherwise the Disconnected event of the socket client is only raised sporadically (race condition)
            if (cancellationToken.IsCancellationRequested) return;

            _logger?.LogDebug("Raise event \"{EventName}\".", eventName);

            var invocationList = @event.GetInvocationList();
            Task.Run(() =>
            {
                try
                {
                    foreach (var handler in invocationList)
                    {
                        var handle = (EventHandler<TEventArgs>)handler;
                        handle(sender, e);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error in \"{EventName}\" event handlers.", eventName);
                }
            }, CancellationToken.None);
        }
    }
}
