// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket.Contracts;
using Jung.SimpleWebSocket.Wrappers;
using System.Net.WebSockets;

namespace Jung.SimpleWebSocket.Helpers;

internal class WebSocketHelper
{
    internal virtual IWebSocket CreateFromStream(Stream stream, bool isServer, string? protocol, TimeSpan keepAliveInterval)
    {
        var webSocket = WebSocket.CreateFromStream(stream, isServer, protocol, keepAliveInterval);
        return new WebSocketWrapper(webSocket);
    }
}
