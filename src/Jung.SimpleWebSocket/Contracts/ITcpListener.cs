// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket.Models;

namespace Jung.SimpleWebSocket.Contracts
{
    internal interface ITcpListener : IDisposable
    {
        bool IsListening { get; }

        void Start();
        Task<WebSocketServerClient> AcceptTcpClientAsync(CancellationToken cancellationToken);
        void Stop();
    }
}
