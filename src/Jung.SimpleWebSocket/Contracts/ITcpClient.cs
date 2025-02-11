// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using System.Net;
using System.Net.Sockets;

namespace Jung.SimpleWebSocket.Contracts
{
    internal interface ITcpClient : IDisposable
    {
        /// <inheritdoc cref="Socket.RemoteEndPoint" />
        EndPoint? RemoteEndPoint { get; }

        /// <inheritdoc cref="TcpClient.GetStream" />
        INetworkStream GetStream();
    }
}