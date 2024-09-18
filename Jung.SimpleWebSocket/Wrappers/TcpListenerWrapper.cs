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
        public new async Task<ITcpClient> AcceptTcpClientAsync(CancellationToken cancellationToken)
        {
            var client = await base.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
            return new TcpClientWrapper(client);
        }
    }
}