// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using System.Net.WebSockets;

namespace Jung.SimpleWebSocket.Contracts
{
    internal interface IWebSocket : IDisposable
    {
        WebSocketState State { get; }
        WebSocketCloseStatus? CloseStatus { get; }
        string? CloseStatusDescription { get; }

        Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken);
        Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken);
        ValueTask SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken);
    }
}
