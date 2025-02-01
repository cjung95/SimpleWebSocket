using Jung.SimpleWebSocket;
using Jung.SimpleWebSocket.Contracts;
using Jung.SimpleWebSocket.Flows;
using Jung.SimpleWebSocket.Models;
using Jung.SimpleWebSocketTest.Mock;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System.Text;

namespace Jung.SimpleWebSocketTest
{
    [TestFixture]
    internal class ClientHandlingFlowTest
    {
        private ILogger<SimpleWebSocketServer> _logger;

        [SetUp]
        public void SetUp()
        {
            var loggerHelper = new ILoggerMockHelper<SimpleWebSocketServer>("Server");
            _logger = loggerHelper.Logger;
        }

        private ClientHandlingFlow SetupClientHandlingFlow(object serverOptions, List<WebSocketServerClient>? activeClients = null)
        {
            var tcpListener = new Mock<ITcpListener>();
            var serverMoq = new Mock<SimpleWebSocketServer>(serverOptions, tcpListener.Object, _logger);
            if (activeClients != null)
            {
                foreach (var client in activeClients)
                {
                    serverMoq.Object.ActiveClients.TryAdd(client.Id, client);
                }
            }

            var tcpClientMoq = new Mock<ITcpClient>();
            var serverClientMoq = new WebSocketServerClient(tcpClientMoq.Object);

            return new ClientHandlingFlow(serverMoq.Object, serverClientMoq, CancellationToken.None);
        }

        private string CreateUpgradeRequest()
        {
            var sb = new StringBuilder();
            sb.Append("GET /chat HTTP/1.1\r\n" +
             "Host: localhost:8080\r\n" +
             "Upgrade: websocket\r\n" +
             "Connection: Upgrade\r\n" +
             "Sec-WebSocket-Key: x3JJHMbDL1EzLkh9GBhXDw==\r\n" +
             "Sec-WebSocket-Version: 13\r\n");

            sb.Append("\r\n\r\n");
            return sb.ToString();
        }
    }
}
