// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using System.Net;

namespace Jung.SimpleWebSocket.Contracts
{
    internal interface ITcpClient : IDisposable
    {
        EndPoint? RemoteEndPoint { get; }
        bool IsConnected { get; }

        INetworkStream GetStream();

        Task ConnectAsync(string host, int port);
    }
}