// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket.Contracts;
using Jung.SimpleWebSocket.Models;
using Jung.SimpleWebSocket.Models.Messages;

namespace Jung.SimpleWebSocket.MessageProcessors
{
    internal class AccumulateBinaryInMemoryProcessor(SimpleWebSocketBaseOptions options, IMessageDispatcher messageDispatcher) : IBinaryMessageProcessor
    {
        private readonly SimpleWebSocketBaseOptions _options = options;
        private readonly IMessageDispatcher _messageDispatcher = messageDispatcher;
        private MemoryStream? _binaryStream;

        public Task ProcessBinaryMessageChunkAsync(byte[] buffer, int chunkSize, CancellationToken cancellationToken)
        {
            _binaryStream ??= new MemoryStream(Math.Min(chunkSize, _options.InMemoryInitialBuffer));
            _binaryStream.Write(buffer, 0, chunkSize);
            return Task.CompletedTask;
        }

        public Task CompleteMessageAsync(SimpleWebSocketBase webSocketBase, CancellationToken cancellationToken)
        {
            if (_binaryStream == null)
            {
                // This should never happen, but just in case
                throw new InvalidOperationException("Binary stream unexpectedly null for binary message");
            }

            var bytes = _binaryStream.ToArray();

            // Dispose the MemoryStream so its internal buffer can be collected sooner.
            _binaryStream.Dispose();

            _messageDispatcher.Dispatch(webSocketBase, new BinaryReceivedMessage(bytes));
            return Task.CompletedTask;
        }
    }
}
