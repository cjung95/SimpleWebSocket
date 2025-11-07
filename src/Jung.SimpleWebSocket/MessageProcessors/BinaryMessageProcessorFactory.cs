// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket.Contracts;
using Jung.SimpleWebSocket.Models;

namespace Jung.SimpleWebSocket.MessageProcessors
{
    internal class BinaryMessageProcessorFactory(SimpleWebSocketBaseOptions options, IMessageDispatcher messageDispatcher)
    {
        private readonly SimpleWebSocketBaseOptions _options = options;
        private readonly IMessageDispatcher _messageDispatcher = messageDispatcher;

        public IBinaryMessageProcessor Create()
        {
            return _options.MessageProcessingMode switch
            {
                BinaryMessageProcessingMode.InMemory => new AccumulateBinaryInMemoryProcessor(_options, _messageDispatcher),
                BinaryMessageProcessingMode.StreamBinaryToFile => new StreamBinaryToFileProcessor(_options, _messageDispatcher),
                _ => throw new NotSupportedException($"Binary message processing mode '{Enum.GetName(_options.MessageProcessingMode)}' is not supported."),
            };
        }
    }
}
