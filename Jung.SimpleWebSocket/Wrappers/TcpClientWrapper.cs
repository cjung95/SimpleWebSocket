// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket.Contracts;
using System.Net;
using System.Net.Sockets;

namespace Jung.SimpleWebSocket.Wrappers
{
    internal class TcpClientWrapper : ITcpClient
    {
        private readonly TcpClient _client;

        public TcpClientWrapper(TcpClient client)
        {
            _client = client;
        }

        public TcpClientWrapper()
        {
            _client = new TcpClient();
        }
        public EndPoint? RemoteEndPoint => _client.Client.RemoteEndPoint;

        public INetworkStream GetStream()
        {
            var stream = _client.GetStream();
            return new NetworkStreamWrapper(stream);
        }

        public Task ConnectAsync(string host, int port)
        {
            return _client.ConnectAsync(host, port);
        }

        public void Dispose()
        {
            _client?.Dispose();
        }
    }
}
