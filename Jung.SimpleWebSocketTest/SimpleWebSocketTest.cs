// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket;
using NUnit.Framework;
using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;

// internals of the simple web socket are visible to the test project
// because of the InternalsVisibleTo attribute in the AssemblyInfo.cs

namespace Jung.SimpleWebSocketTest
{
    [TestFixture]
    public class SimpleWebSocketTest
    {


        [Test]
        [Platform("Windows7,Windows8,Windows8.1,Windows10", Reason = "This test establishes a TCP client-server connection using SimpleWebSocket, which relies on specific networking features and behaviors that are only available and consistent on Windows platforms. Running this test on non-Windows platforms could lead to inconsistent results or failures due to differences in networking stack implementations.")]
        public async Task TestClientServerConnection_ShouldSendAndReceiveHelloWorld()
        {
            // Arrange
            using var server = new SimpleWebSocketServer(IPAddress.Any, 8010);
            using var client = new SimpleWebSocketClient(IPAddress.Loopback.ToString(), 8010, "/");


            const string Message = "Hello World";
            const string ClosingStatusDescription = "closing status test description";
            string receivedMessage = string.Empty;
            string receivedClosingDescription = string.Empty;

            var messageResetEvent = new ManualResetEvent(false);
            var disconnectResetEvent = new ManualResetEvent(false);
            var connectResetEvent = new ManualResetEvent(false);

            server.MessageReceived += (receivedMessageArgs) =>
            {
               server.SendMessageAsync(receivedMessageArgs.ClientId, receivedMessageArgs.Message).Wait();
            };

            server.ClientConnected += (obj) =>
            {
                Debug.WriteLine("Client connected");
                connectResetEvent.Set();
            };

            server.ClientDisconnected += (obj) =>
            {
                receivedClosingDescription = obj.ClosingStatusDescription;
                disconnectResetEvent.Set();
            };

            client.MessageReceived += (message) =>
            {
                receivedMessage = message;
                messageResetEvent.Set();
            };

            // Act
            server.Start();
            await client.ConnectAsync();

            WaitForManualResetEventOrThrow(connectResetEvent);

            await client.SendMessageAsync(Message);
            WaitForManualResetEventOrThrow(messageResetEvent);

            await client.DisconnectAsync(ClosingStatusDescription);
            WaitForManualResetEventOrThrow(disconnectResetEvent);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(receivedMessage, Is.EqualTo(Message));
                Assert.That(receivedClosingDescription, Is.EqualTo(ClosingStatusDescription));
            });
        }

        [Test]
        [Platform("Windows7,Windows8,Windows8.1,Windows10", Reason = "This test establishes a TCP client-server connection using SimpleWebSocket, which relies on specific networking features and behaviors that are only available and consistent on Windows platforms. Running this test on non-Windows platforms could lead to inconsistent results or failures due to differences in networking stack implementations.")]
        public async Task TestMultipleClientServerConnection_ShouldSendAndReceiveHelloWorld()
        {
            // Arrange
            using var server = new SimpleWebSocketServer(IPAddress.Any, 8010);
            List<SimpleWebSocketClient> clients = [];
            var message = "Hello World";
            const int clientsCount = 1000;

            object clientConnectLock = new();
            var clientsConnectedCount = 0;

            object receivedMessageLock = new();
            var receivedMessagesCount = 0;

            object clientDisconnectLock = new();
            var clientsDisconnectedCount = 0;

            server.MessageReceived += (receivedMessageArgs) =>
            {
                lock (receivedMessageLock)
                {
                    if (receivedMessageArgs.Message == message)
                    {
                        receivedMessagesCount++;
                    }
                }
            };

            server.ClientConnected += (obj) =>
            {
                lock (receivedMessageLock)
                {
                    clientsConnectedCount++;
                }
            };

            server.ClientDisconnected += (obj) =>
            {
                lock (clientDisconnectLock)
                {
                    clientsDisconnectedCount++;
                }
            };

            // Act
            server.Start(CancellationToken.None);

            for (int i = 0; i < clientsCount; i++)
            {
                var client = new SimpleWebSocketClient(IPAddress.Loopback.ToString(), 8010, "/");
                clients.Add(client);
                await client.ConnectAsync();
            }

            for (int i = 0; i < clientsCount; i++)
            {
                await clients[i].SendMessageAsync(message);
            }

            for (int i = 0; i < clientsCount; i++)
            {
                await clients[i].DisconnectAsync();
            }

            await Task.Delay(1);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(clientsConnectedCount, Is.EqualTo(clientsCount));
                Assert.That(receivedMessagesCount, Is.EqualTo(clientsCount));
                Assert.That(clientsDisconnectedCount, Is.EqualTo(clientsCount));
            });
        }


        private static void WaitForManualResetEventOrThrow(ManualResetEvent manualResetEvent, int millisecondsTimeout = 100, [CallerArgumentExpression(nameof(manualResetEvent))] string? resetEventName = null)
        {
            if (!manualResetEvent.WaitOne(millisecondsTimeout))
            {
                throw new TimeoutException($"Timeout waiting for {resetEventName}");
            }
        }
    }
}