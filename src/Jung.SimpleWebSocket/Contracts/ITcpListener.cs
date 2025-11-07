// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket.Wrappers;

namespace Jung.SimpleWebSocket.Contracts
{
    internal interface ITcpListener : IDisposable
    {
        bool IsListening { get; }
        void Start();
        Task<TcpClientWrapper> AcceptTcpClientAsync(CancellationToken cancellationToken);
        void Stop();
    }
}
