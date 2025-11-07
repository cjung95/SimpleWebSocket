// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket.Models.Messages;

namespace Jung.SimpleWebSocket.Contracts
{
    /// <summary>
    /// Defines the contract for a message dispatcher based on <see cref="MessageBase"/>.
    /// </summary>
    internal interface IMessageDispatcher
    {
        /// <summary>
        /// Registers a handler for a specific message type.
        /// </summary>
        /// <typeparam name="T">The type of message to handle.</typeparam>
        /// <param name="handler">The handler action to register.</param>
        void RegisterHandler<T>(Action<SimpleWebSocketBase, T> handler) where T : MessageBase;


        /// <summary>
        /// Dispatches the given message to the appropriate handler based on its type.
        /// </summary>
        /// <param name="sender">The sender of the message.</param>
        /// <param name="message">The message to send.</param>
        void Dispatch(SimpleWebSocketBase sender, MessageBase message);
    }
}
