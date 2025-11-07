// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket.Contracts;
using System.Net;
using System.Net.Sockets;

namespace Jung.SimpleWebSocket.Wrappers
{
    internal class TcpListenerWrapper(IPAddress localIpAddress, int port) : TcpListener(localIpAddress, port), ITcpListener
    {
        public bool IsListening => Active;
        public new async Task<TcpClientWrapper> AcceptTcpClientAsync(CancellationToken cancellationToken)
        {
            var tcpClient = await WaitAndWrap(AcceptSocketAsync(cancellationToken)).ConfigureAwait(false);

            static async ValueTask<TcpClientWrapper> WaitAndWrap(ValueTask<Socket> task) =>
                new TcpClientWrapper(await task.ConfigureAwait(false));

            return tcpClient;
        }
    }
}