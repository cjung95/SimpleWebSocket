// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket;
using Jung.SimpleWebSocket.Contracts;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;

// internals of the simple websocket are visible to the test project
// because of the InternalsVisibleTo attribute in the AssemblyInfo.cs

namespace Jung.SimpleWebSocketTest
{
    [TestFixture]
    public class SimpleWebSocketTest
    {
        [Test]
        public async Task TestSendingAndReceivingHelloWorld()
        {
            // Arrange
            using var server = new SimpleWebSocketServer(IPAddress.Any, 8010);
            using var client = new SimpleWebSocketClient(IPAddress.Loopback.ToString(), 8010, "/");

            string _receivedMessage = string.Empty;
            var messageResetEvent = new ManualResetEvent(false);
            var disconnectResetEvent = new ManualResetEvent(false);
            var connectResetEvent = new ManualResetEvent(false);

            server.MessageReceived += (message) =>
            {
                _receivedMessage = message;
                messageResetEvent.Set();
            };

            server.ClientConnected += (obj) =>
            {
                Debug.WriteLine("Client connected");
                connectResetEvent.Set();
            };

            server.ClientDisconnected += (obj) =>
            {
                Debug.WriteLine("Client disconnected");
                disconnectResetEvent.Set();
            };

            // Act
            server.Start(CancellationToken.None);
            await client.ConnectAsync(CancellationToken.None);

            WaitForManualResetEventOrThrow(connectResetEvent, 100);


            await client.SendMessageAsync("Hello World", CancellationToken.None);
            WaitForManualResetEventOrThrow(messageResetEvent, 10);

            await client.DisconnectAsync();
            WaitForManualResetEventOrThrow(disconnectResetEvent, 1);

            // Assert
            Assert.That(_receivedMessage, Is.EqualTo("Hello World"));
        }

        private void WaitForManualResetEventOrThrow(ManualResetEvent manualResetEvent, int timeout, [CallerArgumentExpression(nameof(manualResetEvent))] string? resetEventName = null)
        {
            if (!manualResetEvent.WaitOne(timeout))
            {
                throw new TimeoutException($"Timeout waiting for {resetEventName}");
            }
        }
    }
}