// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket.Exceptions;
using Jung.SimpleWebSocket.Models;
using NUnit.Framework;

namespace Jung.SimpleWebSocketTest
{
    [TestFixture]
    internal class WebContextTest
    {
        private const string WebSocketUpgradeRequest =
            "GET /chat HTTP/1.1\r\n" +
            "Host: localhost:8080\r\n" +
            "Upgrade: websocket\r\n" +
            "Connection: Upgrade\r\n" +
            "Sec-WebSocket-Key: x3JJHMbDL1EzLkh9GBhXDw==\r\n" +
            "Sec-WebSocket-Version: 13\r\n\r\n";

        private WebContext _requestContext;

        [SetUp]
        public void SetUp()
        {
            _requestContext = new WebContext(WebSocketUpgradeRequest);
        }

        [Test]
        public void HostName_ShouldReturnCorrectHostName()
        {
            // Act
            var hostName = _requestContext.HostName;

            // Assert
            Assert.That(hostName, Is.EqualTo("localhost"));
        }

        [Test]
        public void HostName_ShouldThrowException_WhenHostHeaderIsMissing()
        {
            // Arrange
            var invalidRequest = new WebContext("GET / HTTP/1.1\r\n\r\n");

            // Act & Assert
            var ex = Assert.Throws<WebSocketServerException>(() => { var hostName = invalidRequest.HostName; });
            Assert.That(ex.Message, Is.EqualTo("Host header is missing"));
        }

        [Test]
        public void Port_ShouldReturnCorrectPort()
        {
            // Act
            var port = _requestContext.Port;

            // Assert
            Assert.That(port, Is.EqualTo(8080));
        }

        [Test]
        public void Port_ShouldReturnDefaultPort_WhenPortIsNotSpecified()
        {
            // Arrange
            var requestWithoutPort = new WebContext("GET / HTTP/1.1\r\nHost: localhost\r\n\r\n");

            // Act
            var port = requestWithoutPort.Port;

            // Assert
            Assert.That(port, Is.EqualTo(80));
        }

        [Test]
        public void RequestPath_ShouldReturnCorrectRequestPath()
        {
            // Act
            var requestPath = _requestContext.RequestPath;

            // Assert
            Assert.That(requestPath, Is.EqualTo("/chat"));
        }

        [Test]
        public void RequestPath_ShouldThrowException_WhenRequestLineIsMissing()
        {
            // Arrange
            var invalidRequest = new WebContext("\r\n");

            // Act & Assert
            var ex = Assert.Throws<WebSocketServerException>(() => { var requestPath = invalidRequest.RequestPath; });
            Assert.That(ex.Message, Is.EqualTo("Status line is missing"));
        }

        [Test]
        public void IsWebSocketRequest_ShouldReturnTrue_WhenHeadersContainWebSocketUpgrade()
        {
            // Act
            var result = _requestContext.IsWebSocketRequest;

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void IsWebSocketRequest_ShouldReturnFalse_WhenHeadersDoNotContainWebSocketUpgrade()
        {
            // Arrange
            var content = "GET /chat HTTP/1.1\r\n" +
                          "Host: server.example.com\r\n" +
                          "Connection: keep-alive\r\n";
            var context = new WebContext(content);

            // Act
            var result = context.IsWebSocketRequest;

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void ContainsHeader_ShouldReturnTrue_WhenHeaderContainsValue()
        {
            // Arrange
            var content = "GET /chat HTTP/1.1\r\n" +
                          "Host: server.example.com\r\n" +
                          "Custom-Header: value1, value2\r\n";
            var context = new WebContext(content);

            // Act
            var result = context.ContainsHeader("Custom-Header", "value1");

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void ContainsHeader_ShouldReturnFalse_WhenHeaderDoesNotContainValue()
        {
            // Arrange
            var content = "GET /chat HTTP/1.1\r\n" +
                          "Host: server.example.com\r\n" +
                          "Custom-Header: value1, value2\r\n";
            var context = new WebContext(content);

            // Act
            var result = context.ContainsHeader("Custom-Header", "value3");

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void StatusLine_ShouldReturnFirstLineOfContent()
        {
            // Arrange
            var content = "GET /chat HTTP/1.1\r\n" +
                          "Host: server.example.com\r\n";
            var context = new WebContext(content);

            // Act
            var result = context.StatusLine;

            // Assert
            Assert.That(result, Is.EqualTo("GET /chat HTTP/1.1"));
        }

        [Test]
        public void StatusLine_ShouldThrowException_WhenContentIsEmpty()
        {
            // Arrange
            var context = new WebContext(string.Empty);

            // Act & Assert
            Assert.Throws<WebSocketServerException>(() => { var _ = context.StatusLine; });
        }

        [Test]
        public void RequestLine_ShouldReturnFirstLineOfContent()
        {
            // Arrange
            var content = "GET /chat HTTP/1.1\r\n" +
                          "Host: server.example.com\r\n";
            var context = new WebContext(content);

            // Act
            var result = context.RequestLine;

            // Assert
            Assert.That(result, Is.EqualTo("GET /chat HTTP/1.1"));
        }

        [Test]
        public void RequestLine_ShouldThrowException_WhenContentIsEmpty()
        {
            // Arrange
            var context = new WebContext(string.Empty);

            // Act & Assert
            Assert.Throws<WebSocketServerException>(() => { var _ = context.RequestLine; });
        }
    }
}