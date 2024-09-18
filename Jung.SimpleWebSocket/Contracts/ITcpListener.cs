// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

namespace Jung.SimpleWebSocket.Contracts
{
    internal interface ITcpListener : IDisposable
    {
        bool IsListening { get; }

        void Start();
        Task<ITcpClient> AcceptTcpClientAsync(CancellationToken cancellationToken);
        void Stop();
    }
}
