// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket.Contracts;
using System.Net.Sockets;

namespace Jung.SimpleWebSocket.Wrappers
{
    internal class NetworkStreamWrapper(NetworkStream stream) : INetworkStream
    {
        public bool DataAvailable => stream.DataAvailable;

        public Stream Stream => stream;

        public void Dispose()
        {
            stream.Dispose();
        }

        public async ValueTask<int> ReadAsync(byte[] buffer, CancellationToken cancellationToken)
        {
            return await stream.ReadAsync(buffer, cancellationToken);
        }

        public ValueTask WriteAsync(byte[] responseBytes, CancellationToken cancellationToken)
        {
            return stream.WriteAsync(responseBytes, cancellationToken);
        }
    }
}
