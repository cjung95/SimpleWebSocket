// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

namespace Jung.SimpleWebSocket.Models.EventArguments
{
    /// <summary>
    /// Event arguments for a binary message that was saved to disk (streaming mode).
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="ClientBinaryMessageSavedArgs"/> class.
    /// </remarks>
    /// <param name="stream">
    /// An open readable <see cref="Stream"/> positioned at the beginning of the message data.
    /// The consumer may read this stream. Ownership semantics are documented below.
    /// </param>
    /// <param name="length">Total length of the message in bytes.</param>
    /// <param name="tempFilePath">
    /// The path to the saved temporary file.
    /// </param>
    /// <param name="completeProcessingAsync">
    /// A callback provided by the server that performs cleanup when the consumer is finished reading.
    /// This callback will be invoked the first time <see cref="CompleteProcessingAsync"/> is called.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> or <paramref name="completeProcessingAsync"/> is null.</exception>
    public sealed class BinaryMessageSavedArgs(Stream stream, long length, string tempFilePath, Func<ValueTask> completeProcessingAsync)
    {
        /// <summary>
        /// Open readable stream that contains the binary message.
        /// </summary>
        public Stream Stream { get; } = stream ?? throw new ArgumentNullException(nameof(stream));

        /// <summary>
        /// Total length of the message in bytes.
        /// </summary>
        public long Length { get; } = length;

        /// <summary>
        /// The path to the saved temporary file.
        /// </summary>
        public string TempFilePath { get; } = tempFilePath;

        // Backing callback. Will be nulled out atomically when invoked to ensure idempotency.
        private Func<ValueTask>? _completeProcessing = completeProcessingAsync ?? throw new ArgumentNullException(nameof(completeProcessingAsync));

        /// <summary>
        /// Signals that the consumer has finished processing the stream and the server may clean up resources.
        /// This method is idempotent: only the first caller will trigger the server-provided cleanup callback.
        /// </summary>
        /// <returns>A <see cref="ValueTask"/> that completes when cleanup is finished.</returns>
        public ValueTask CompleteProcessingAsync()
        {
            // We use a value task here to avoid allocations in the common case where the callback completes synchronously.
            // This is a common pattern for high-performance async APIs.

            // Atomically take the callback. If it's already null, cleanup has already been requested.
            var callback = Interlocked.Exchange(ref _completeProcessing, null);
            if (callback == null)
            {
                // Already completed; return completed task
                return ValueTask.CompletedTask;
            }

            try
            {
                return callback();
            }
            catch (Exception ex)
            {
                // If the callback throws, return a faulted ValueTask
                return ValueTask.FromException(ex);
            }
        }
    }
}