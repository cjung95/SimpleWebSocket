// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket.Contracts;
using System.Net;
using System.Net.Sockets;

namespace Jung.SimpleWebSocket.Wrappers
{
    internal class TcpListenerWrapper(IPAddress localIpAddress, int port) : ITcpListener
    {
        private readonly TcpListener _tcpListener = new(localIpAddress, port);

        public void Start() => _tcpListener.Start();

        public void Stop() => _tcpListener.Stop();

        public void Dispose()
        {
            _tcpListener?.Dispose();
        }
        public async Task<ITcpClient> AcceptTcpClientAsync(CancellationToken cancellationToken)
        {
            var client = await _tcpListener.AcceptTcpClientAsync(cancellationToken);
            return new TcpClientWrapper(client);
        }
    }
}
