// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket.Contracts;
using System.Net.WebSockets;

namespace Jung.SimpleWebSocket.Wrappers
{
    internal class WebSocketWrapper(WebSocket webSocket) : IWebSocket
    {
        public WebSocketState State => webSocket.State;
        public WebSocketCloseStatus? CloseStatus => webSocket.CloseStatus;
        public string? CloseStatusDescription => webSocket.CloseStatusDescription;

        public ValueTask SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            return webSocket.SendAsync(buffer, messageType, endOfMessage, cancellationToken);
        }

        public Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            return webSocket.ReceiveAsync(buffer, cancellationToken);
        }

        public Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
        {
            return webSocket.CloseAsync(closeStatus, statusDescription, cancellationToken);
        }

        public void Dispose()
        {
            webSocket.Dispose();
        }
    }
}
