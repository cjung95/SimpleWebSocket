// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

namespace Jung.SimpleWebSocket.MessageProcessors
{
    internal interface IBinaryMessageProcessor
    {
        public Task CompleteMessageAsync(SimpleWebSocketBase webSocketBase, CancellationToken cancellationToken);
        public Task ProcessBinaryMessageChunkAsync(byte[] buffer, int chunkSize, CancellationToken cancellationToken);
    }
}
