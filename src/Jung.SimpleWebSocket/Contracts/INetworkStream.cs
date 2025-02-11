// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

namespace Jung.SimpleWebSocket.Contracts
{
    internal interface INetworkStream : IDisposable
    {
        bool DataAvailable { get; }
        Stream Stream { get; }

        ValueTask<int> ReadAsync(byte[] buffer, CancellationToken cancellationToken);
        ValueTask WriteAsync(byte[] responseBytes, CancellationToken cancellationToken);
    }
}
