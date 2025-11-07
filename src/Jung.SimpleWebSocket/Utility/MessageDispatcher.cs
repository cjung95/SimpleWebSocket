// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket.Contracts;
using Jung.SimpleWebSocket.Models.Messages;
using System.Diagnostics;

namespace Jung.SimpleWebSocket.Utility
{
    /// <summary>
    /// A dispatcher for messages based on <see cref="MessageBase"/> 
    /// </summary>
    internal class MessageDispatcher : IMessageDispatcher
    {
        private readonly Dictionary<Type, Action<SimpleWebSocketBase, MessageBase>> _handlers = [];

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException">Thrown if the handler is null.</exception>"
        public void RegisterHandler<T>(Action<SimpleWebSocketBase, T> handler) where T : MessageBase
        {
            ArgumentNullException.ThrowIfNull(handler);

            // Wrap the handler so it matches Action<SimpleWebSocketBase, MessageBase>
            _handlers[typeof(T)] = (sender, message) => handler(sender, (T)message);
        }

        /// <inheritdoc/>
        public void Dispatch(SimpleWebSocketBase sender, MessageBase message)
        {
            ArgumentNullException.ThrowIfNull(message);

            if (_handlers.TryGetValue(message.GetType(), out var handler))
            {
                handler.Invoke(sender, message);
            }
            else
            {
                // No handler registered for this message type
                Debug.Fail($"No handler registered for message type {message.GetType().Name}");
            }
        }
    }
}
