// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket.Contracts;
using Jung.SimpleWebSocket.Models;
using Jung.SimpleWebSocket.Models.Messages;

namespace Jung.SimpleWebSocket.MessageProcessors
{
    internal class StreamBinaryToFileProcessor : IBinaryMessageProcessor
    {
        private readonly SimpleWebSocketBaseOptions _options;
        private readonly IMessageDispatcher _messageDispatcher;
        private readonly string _tempFilePath = string.Empty;
        private readonly FileStream _fileStream;

        public StreamBinaryToFileProcessor(SimpleWebSocketBaseOptions options, IMessageDispatcher messageDispatcher)
        {
            _options = options;
            _messageDispatcher = messageDispatcher;

            // create a temp file for this binary message.
            _tempFilePath = Path.Combine(Path.GetTempPath(), $"wsbin_{Guid.NewGuid():N}.tmp");

            var fileOptions = _options.AutoDeleteTempFiles
                ? FileOptions.DeleteOnClose | FileOptions.SequentialScan
                : FileOptions.SequentialScan;

            _fileStream = new FileStream(_tempFilePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, _options.FileStreamBufferSize, fileOptions);
        }


        public async Task ProcessBinaryMessageChunkAsync(byte[] buffer, int chunkSize, CancellationToken cancellationToken)
        {
            if (_fileStream == null)
            {
                throw new InvalidOperationException("File stream unexpectedly null for binary message");
            }

            // write chunk to file
            await _fileStream.WriteAsync(buffer.AsMemory(0, chunkSize), cancellationToken).ConfigureAwait(false);
        }

        public async Task CompleteMessageAsync(SimpleWebSocketBase webSocketBase, CancellationToken cancellationToken)
        {
            // Ensure data is flushed
            await _fileStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            var length = _fileStream.Length;

            // Prepare an open readable stream positioned at beginning
            _fileStream.Seek(0, SeekOrigin.Begin);

            // Create a cleanup callback that will dispose the stream and delete the file if configured
            ValueTask cleanup()
            {
                try
                {
                    // Dispose the FileStream
                    // if DeleteOnClose was used the file will be removed when disposed
                    _fileStream.Dispose();

                    return ValueTask.CompletedTask;
                }
                catch (Exception ex)
                {
                    return ValueTask.FromException(ex);
                }
            }
            _messageDispatcher.Dispatch(webSocketBase, new BinaryMessageSavedMessage(_fileStream, length, _tempFilePath, cleanup));
        }
    }
}
