// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket.Contracts;
using Jung.SimpleWebSocket.Models;
using System.Net;
using System.Net.Sockets;

namespace Jung.SimpleWebSocket.Wrappers
{
    internal class TcpListenerWrapper(IPAddress localIpAddress, int port) : TcpListener(localIpAddress, port), ITcpListener
    {
        public bool IsListening => Active;
        public new async Task<WebSocketServerClient> AcceptTcpClientAsync(CancellationToken cancellationToken)
        {
            var tcpClient = await WaitAndWrap(AcceptSocketAsync(cancellationToken));

            static async ValueTask<TcpClientWrapper> WaitAndWrap(ValueTask<Socket> task) =>
                new TcpClientWrapper(await task.ConfigureAwait(false));

            return new WebSocketServerClient(tcpClient);
        }
    }
}