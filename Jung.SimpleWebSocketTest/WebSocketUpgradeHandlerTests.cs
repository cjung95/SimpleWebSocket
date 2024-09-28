// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket;
using Jung.SimpleWebSocket.Contracts;
using Jung.SimpleWebSocket.Exceptions;
using Jung.SimpleWebSocket.Helpers;
using Jung.SimpleWebSocket.Models;
using Moq;
using NUnit.Framework;
using System.Text;

namespace Jung.SimpleWebSocketTest
{
    public class WebSocketUpgradeHandlerTests
    {
        private readonly Mock<INetworkStream> _mockNetworkStream;
        private readonly Mock<WebSocketHelper> _mockWebSocketHelper;
        private readonly WebSocketUpgradeHandler _socketWrapper;


        public WebSocketUpgradeHandlerTests()
        {
            _mockNetworkStream = new Mock<INetworkStream>();
            _mockWebSocketHelper = new Mock<WebSocketHelper>();
            _socketWrapper = new WebSocketUpgradeHandler(_mockNetworkStream.Object, _mockWebSocketHelper.Object);
        }

        [Test]
        public async Task AwaitContextAsync_ShouldReturnWebContext()
        {
            // Arrange
            var cancellationToken = new CancellationToken();
            var requestString = "GET / HTTP/1.1\r\nHost: localhost\r\n\r\n";
            var requestBytes = Encoding.ASCII.GetBytes(requestString);
            var sequence = new MockSequence();
            _mockNetworkStream.InSequence(sequence).Setup(ns => ns.ReadAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>())).ReturnsAsync(requestBytes.Length).Callback<byte[], CancellationToken>((buffer, ct) => requestBytes.CopyTo(buffer, 0));
            _mockNetworkStream.InSequence(sequence).Setup(ns => ns.ReadAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);

            // Act
            var context = await _socketWrapper.AwaitContextAsync(cancellationToken);

            // Assert
            Assert.That(context, Is.Not.Null);
            Assert.That(context.HostName, Is.EqualTo("localhost"));
        }

        [TestCase("localhost", 8010, "/")]
        [TestCase("ws://192.168.123.123/", 8010, "/chat")]
        [TestCase("wss://10.10.184.12", 8010, "/")]
        public async Task AcceptWebSocketAsync_ShouldSendUpgradeResponse(string hostname, int port, string path)
        {
            // Arrange
            var cancellationToken = new CancellationToken();
            var request = new WebContext(
                $"GET {path} HTTP/1.1\n\r" +
                $"Host: {hostname}:{port}\n\r" +
                $"Connection: upgrade\n\r" +
                $"Upgrade: websocket\n\r" +
                $"Sec-WebSocket-Version: 13\n\r" +
                $"Sec-WebSocket-Key: {Convert.ToBase64String(Guid.NewGuid().ToByteArray())}\n\r\n\r");

            var response = string.Empty;
            _mockNetworkStream.Setup(ns => ns.WriteAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>())).Callback<byte[], CancellationToken>((buffer, ct) => { response = Encoding.UTF8.GetString(buffer); });

            // Act
            await _socketWrapper.AcceptWebSocketAsync(request, Guid.NewGuid().ToString(), cancellationToken);

            // Assert
            Assert.That(response, Does.Contain("HTTP/1.1 101 Switching Protocols"));
        }

        [TestCase("foo, bar", "foo")]
        [TestCase("foo, bar", "bar")]
        [TestCase("foo, bar")]
        public async Task AcceptWebSocketAsync_ShouldSendUpgradeResponseWithCorrectProtocol(string protocolHeaderValue = "", string? serverSubprotocol = null)
        {
            // Arrange
            var cancellationToken = new CancellationToken();
            var request = new WebContext(
                $"GET / HTTP/1.1\n\r" +
                $"Host: localhost\n\r" +
                $"Connection: upgrade\n\r" +
                $"Upgrade: websocket\n\r" +
                $"Sec-WebSocket-Version: 13\n\r" +
                $"Sec-WebSocket-Key: {Convert.ToBase64String(Guid.NewGuid().ToByteArray())}\n\r" +
                $"Sec-WebSocket-Protocol: {protocolHeaderValue}\n\r\n\r");

            var response = string.Empty;
            _mockNetworkStream.Setup(ns => ns.WriteAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>())).Callback<byte[], CancellationToken>((buffer, ct) => { response = Encoding.UTF8.GetString(buffer); });

            // Act
            await _socketWrapper.AcceptWebSocketAsync(request, Guid.NewGuid().ToString(), serverSubprotocol, cancellationToken);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(response, Does.Contain("HTTP/1.1 101 Switching Protocols"));
                if (serverSubprotocol != null)
                {
                    Assert.That(response, Does.Contain($"Sec-WebSocket-Protocol: {serverSubprotocol}"));
                }
            });
        }

        [Test]
        public async Task SendUpgradeRequestAsync_ShouldSendRequestHeaders()
        {
            // Arrange
            var cancellationToken = new CancellationToken();
            var requestContext = WebContext.CreateRequest(hostName: "localhost", port: 80, requestPath: "/");
            var response = string.Empty;
            _mockNetworkStream.Setup(ns => ns.WriteAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>())).Callback<byte[], CancellationToken>((buffer, ct) => { response = Encoding.UTF8.GetString(buffer); });


            // Act
            await _socketWrapper.SendUpgradeRequestAsync(requestContext, cancellationToken);

            // Assert
            Assert.That(response, Does.Contain("GET / HTTP/1.1"));
        }

        [Test]
        public void ValidateUpgradeResponse_ShouldThrowException_WhenStatusLineIsInvalid()
        {
            // Arrange
            var responseContext = new WebContext("HTTP/1.1 200 OK\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: dummy\r\n\r\n");
            var requestContext = WebContext.CreateRequest("localhost", 80, "/");

            // Act & Assert
            var ex = Assert.Throws<WebSocketUpgradeException>(() => WebSocketUpgradeHandler.ValidateUpgradeResponse(responseContext, requestContext));
            Assert.That(ex.Message, Is.EqualTo("Invalid status code, expected '101 Switching Protocols'."));
        }

        [Test]
        public void ValidateUpgradeResponse_ShouldThrowException_WhenHeadersAreInvalid()
        {
            // Arrange
            var responseContext = new WebContext("HTTP/1.1 101 Switching Protocols\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: dummy\r\n\r\n");
            var requestContext = WebContext.CreateRequest("localhost", 80, "/");

            // Act & Assert
            var ex = Assert.Throws<WebSocketUpgradeException>(() => WebSocketUpgradeHandler.ValidateUpgradeResponse(responseContext, requestContext));
            Assert.That(ex.Message, Is.EqualTo("Invalid 'Upgrade' or 'Connection' header."));
        }

        [Test]
        public void ValidateUpgradeResponse_ShouldThrowException_WhenSecWebSocketAcceptIsMissing()
        {
            // Arrange
            var responseContext = new WebContext("HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n\r\n");
            var requestContext = WebContext.CreateRequest("localhost", 80, "/");

            // Act & Assert
            var ex = Assert.Throws<WebSocketUpgradeException>(() => WebSocketUpgradeHandler.ValidateUpgradeResponse(responseContext, requestContext));
            Assert.That(ex.Message, Is.EqualTo("Missing 'Sec-WebSocket-Accept' header."));
        }

        [Test]
        public void ValidateUpgradeResponse_ShouldThrowException_WhenSecWebSocketAcceptIsInvalid()
        {
            // Arrange
            var responseContext = new WebContext("HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: invalid\r\n\r\n");
            var requestContext = WebContext.CreateRequest("localhost", 80, "/");
            requestContext.Headers.Add("Sec-WebSocket-Key", "dummy");

            // Act & Assert
            var ex = Assert.Throws<WebSocketUpgradeException>(() => WebSocketUpgradeHandler.ValidateUpgradeResponse(responseContext, requestContext));
            Assert.That(ex.Message, Is.EqualTo("Invalid 'Sec-WebSocket-Accept' value."));
        }

        [Test]
        public void ProcessWebSocketProtocolHeader_ShouldReturnServerProtocol()
        {
            // Arrange 
            var clientProtocol = "foo, bar";
            var serverProtocol = "foo";

            // Act
            WebSocketUpgradeHandler.ProcessWebSocketProtocolHeader(clientProtocol, serverProtocol, out var acceptProtocol);
            // Assert
            Assert.That(acceptProtocol, Is.EqualTo("foo"));
        }

        [Test]
        public void ProcessWebSocketProtocolHeader_ShouldThrow_WhenServerProtocolIsEmpty()
        {
            // Arrange 
            var protocol = "foo";

            // Act & Assert
            var ex = Assert.Throws<WebSocketUpgradeException>(() => WebSocketUpgradeHandler.ProcessWebSocketProtocolHeader(null, protocol, out _));
            Assert.That(ex.Message, Does.Contain("The WebSocket client did not request any protocols, but"));

        }

        [Test]
        public void ProcessWebSocketProtocolHeader_ShouldThrow_WhenClientProtocolIsEmpty()
        {
            // Arrange 
            var protocol = "foo, bar";

            // Act & Assert
            WebSocketUpgradeHandler.ProcessWebSocketProtocolHeader(protocol, null, out var acceptProtocol);
            Assert.That(acceptProtocol, Is.EqualTo(string.Empty));
        }
    }
}
