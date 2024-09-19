// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket.Contracts;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;

namespace Jung.SimpleWebSocket.Wrappers
{
    internal class TcpClientWrapper : TcpClient, ITcpClient
    {
        /// <summary>
        /// The internal constructor of the <see cref="TcpClient"/> class that takes a <see cref="Socket"/> as a parameter.
        /// </summary>
        private readonly ConstructorInfo _internalSocketConstructor = typeof(TcpClient).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, [typeof(Socket)])!;

        /// <summary>
        /// Creates a new instance of the <see cref="TcpClientWrapper"/> class.
        /// </summary>
        internal TcpClientWrapper() : base()
        {
        }

        /// <summary>
        /// Creates a new instance of the <see cref="TcpClientWrapper"/> class.
        /// </summary>
        /// <param name="socket">The socket that should be used for the <see cref="TcpClientWrapper"/></param>
        internal TcpClientWrapper(Socket socket)
        {
            try
            {
                _internalSocketConstructor.Invoke(this, [socket]);
            }
            catch (Exception)
            {
                Trace.TraceError("Check the constructor of the TcpClient class!!!");
                throw;
            }
        }

        /// <inheritdoc/>
        public EndPoint? RemoteEndPoint => Client.RemoteEndPoint;

        /// <inheritdoc/>
        public new INetworkStream GetStream()
        {
            var stream = base.GetStream();
            return new NetworkStreamWrapper(stream);
        }
    }
}