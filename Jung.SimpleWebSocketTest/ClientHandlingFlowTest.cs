using Jung.SimpleWebSocket;
using Jung.SimpleWebSocket.Contracts;
using Jung.SimpleWebSocket.Exceptions;
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

        private string CreateUpgradeRequest(string? userId)
        {
            var sb = new StringBuilder();
            sb.Append("GET /chat HTTP/1.1\r\n" +
             "Host: localhost:8080\r\n" +
             "Upgrade: websocket\r\n" +
             "Connection: Upgrade\r\n" +
             "Sec-WebSocket-Key: x3JJHMbDL1EzLkh9GBhXDw==\r\n" +
             "Sec-WebSocket-Version: 13\r\n");

            if (!string.IsNullOrEmpty(userId))
            {
                sb.Append("x-user-id: 6C8D0844-D84F-4AD9-B28D-23B3940887B7");
            }

            sb.Append("\r\n\r\n");
            return sb.ToString();
        }


        [Test]
        public void HandleClientIdentification_NoNewUser_UserIdIsUpdated()
        {
            var userId = "6C8D0844-D84F-4AD9-B28D-23B3940887B7";
            var requestText = CreateUpgradeRequest(userId);
            var serverOptions = new SimpleWebSocketServerOptions
            {
                RememberDisconnectedClients = true,
            };

            var clientHandlingFlow = SetupClientHandlingFlow(serverOptions);

            clientHandlingFlow.Request = new WebContext(requestText);
            clientHandlingFlow.HandleClientIdentification();

            Assert.That(clientHandlingFlow.Client.Id, Is.EqualTo(userId));
        }

        [Test]
        public void HandleClientIdentification_NoNewUser_UserAlreadyConnected()
        {
            // setup 
            var userId = "6C8D0844-D84F-4AD9-B28D-23B3940887B7";
            var activeUsers = new List<WebSocketServerClient>();
            var client = new WebSocketServerClient(DateTime.Now);
            client.UpdateId(userId);
            activeUsers.Add(client);
            var requestText = CreateUpgradeRequest(userId);
            var serverOptions = new SimpleWebSocketServerOptions
            {
                RememberDisconnectedClients = true,
            };

            // act and assert
            var clientHandlingFlow = SetupClientHandlingFlow(serverOptions, activeUsers);

            clientHandlingFlow.Request = new WebContext(requestText);
            Assert.That(() => clientHandlingFlow.HandleClientIdentification(), Throws.Exception.TypeOf<UserNotHandledException>());
        }
    }
}
