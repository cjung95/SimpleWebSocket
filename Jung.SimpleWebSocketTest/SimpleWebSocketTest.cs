// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket;
using Microsoft.Extensions.Logging;
using Moq;
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
        private readonly List<string> _logMessages = [];
        private Mock<ILogger<SimpleWebSocketServer>> _serverLogger;
        private Mock<ILogger<SimpleWebSocketServer>> _clientLogger;

        [OneTimeSetUp]
        public void SetUpOnce()
        {
            Trace.Listeners.Add(new ConsoleTraceListener());
        }

        [SetUp]
        public void SetUp()
        {
            _logMessages.Clear();
            _serverLogger = new Mock<ILogger<SimpleWebSocketServer>>();
            _clientLogger = new Mock<ILogger<SimpleWebSocketServer>>();
            SetUpLogger(_serverLogger, "Server");
            SetUpLogger(_clientLogger, "Client");
        }

        [OneTimeTearDown]
        public void EndTest()
        {
            Trace.Flush();
        }

        private void SetUpLogger<T>(Mock<ILogger<T>> mock, string loggerName)
        {
            mock.Setup(m => m.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()!
            )).Callback(new InvocationAction(invocation =>
            {
                var logLevel = (LogLevel)invocation.Arguments[0];
                var eventId = (EventId)invocation.Arguments[1];
                var state = invocation.Arguments[2];
                var exception = (Exception)invocation.Arguments[3];
                var formatter = invocation.Arguments[4];

                var invokeMethod = formatter.GetType().GetMethod("Invoke");
                var logMessage = invokeMethod!.Invoke(formatter, [state, exception]);
                _logMessages.Add($"[{DateTime.Now:HH:mm:ss:fff}] {loggerName} ({logLevel}): {logMessage}");
            }));
        }

        [Test]
        [Platform("Windows7,Windows8,Windows8.1,Windows10", Reason = "This test establishes a TCP client-server connection using SimpleWebSocket, which relies on specific networking features and behaviors that are only available and consistent on Windows platforms. Running this test on non-Windows platforms could lead to inconsistent results or failures due to differences in networking stack implementations.")]
        public async Task TestClientServerConnection_ShouldSendAndReceiveHelloWorld()
        {
            // Arrange
            using var server = new SimpleWebSocketServer(IPAddress.Any, 8010, _serverLogger.Object);
            using var client = new SimpleWebSocketClient(IPAddress.Loopback.ToString(), 8010, "/", logger: _clientLogger.Object);


            const string Message = "Hello World";
            const string ClosingStatusDescription = "closing status test description";
            string receivedMessage = string.Empty;
            string receivedClosingDescription = string.Empty;
            string exceptionMessage = string.Empty;

            var messageResetEvent = new ManualResetEvent(false);
            var disconnectResetEvent = new ManualResetEvent(false);
            var connectResetEvent = new ManualResetEvent(false);

            server.MessageReceived += (sender, receivedMessageArgs) =>
            {
                server.SendMessageAsync(receivedMessageArgs.ClientId, receivedMessageArgs.Message).Wait();
            };

            server.ClientConnected += (sender, obj) =>
            {
                Debug.WriteLine("Client connected");
                connectResetEvent.Set();
            };

            server.ClientDisconnected += (sender, obj) =>
            {
                receivedClosingDescription = obj.ClosingStatusDescription;
                disconnectResetEvent.Set();
            };

            client.MessageReceived += (sender, obj) =>
            {
                receivedMessage = obj.Message;
                messageResetEvent.Set();
            };

            server.ClientUpgradeRequestReceivedAsync += async (sender, args, cancellationToken) =>
            {
                // Get the IP address of the client
                var IpAddress = (args.Client.RemoteEndPoint as IPEndPoint)?.Address.ToString();
                if (IpAddress == null)
                {
                    args.Handle = false;
                    return;
                }

                // Check the IpAddress against the database
                var isWhitelistedEndPoint = await DbContext_IpAddresses_Contains(IpAddress, cancellationToken);
                if (!isWhitelistedEndPoint)
                {
                    args.ResponseContext.StatusCode = HttpStatusCode.Forbidden;
                    args.ResponseContext.BodyContent = "Connection only possible via local network.";
                    args.Handle = false;
                }
                args.Client.Properties["test"] = "test";
            };

            // Act
            server.Start();
            await client.ConnectAsync();
            WaitForManualResetEventOrThrow(connectResetEvent);

            await client.SendMessageAsync(Message);
            WaitForManualResetEventOrThrow(messageResetEvent);

            await client.DisconnectAsync(ClosingStatusDescription);
            WaitForManualResetEventOrThrow(disconnectResetEvent);

            // test if the server accepts the client again
            var client2 = new SimpleWebSocketClient(IPAddress.Loopback.ToString(), 8010, "/", client.UserId, logger: _clientLogger.Object);
            await client2.ConnectAsync();

            await Task.Delay(100);
            try
            {
                // test if two clients with the same user id can connect
                var client3 = new SimpleWebSocketClient(IPAddress.Loopback.ToString(), 8010, "/", client.UserId, logger: _clientLogger.Object);
                await client3.ConnectAsync();
            }
            catch (Exception exception)
            {
                exceptionMessage = exception.InnerException!.Message;
            }

            await client2.SendMessageAsync("Hello World");

            await server.ShutdownServer(CancellationToken.None);
            _logMessages.ForEach(m => Trace.WriteLine(m));

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(receivedMessage, Is.EqualTo(Message));
                Assert.That(receivedClosingDescription, Is.EqualTo(ClosingStatusDescription));
                Assert.That(exceptionMessage, Does.Contain("User ID already connected"));
            });
        }

        /// <summary>
        /// Fake Async method to simulate a database call to check if the IP address is in the database.
        /// </summary>
        /// <param name="ipAddress">The IP address to check.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a value indicating whether the IP address is in the database.</returns>
        private static async Task<bool> DbContext_IpAddresses_Contains(string ipAddress, CancellationToken cancellationToken)
        {
            await Task.Delay(100, cancellationToken);
            return ipAddress == IPAddress.Loopback.ToString();
        }

        [Test]
        [Platform("Windows7,Windows8,Windows8.1,Windows10", Reason = "This test establishes a TCP client-server connection using SimpleWebSocket, which relies on specific networking features and behaviors that are only available and consistent on Windows platforms. Running this test on non-Windows platforms could lead to inconsistent results or failures due to differences in networking stack implementations.")]
        public async Task TestClientServerConnection_ShouldSendAndReceiveHelloWorld2()
        {
            // Arrange
            using var server = new SimpleWebSocketServer(IPAddress.Any, 8010, _serverLogger.Object);
            using var client = new SimpleWebSocketClient(IPAddress.Loopback.ToString(), 8010, "/", logger: _clientLogger.Object);


            const string Message = "Hello World";
            const string ClosingStatusDescription = "Server is shutting down";
            string receivedMessage = string.Empty;
            string receivedClosingDescription = string.Empty;

            var messageResetEvent = new ManualResetEvent(false);
            var disconnectResetEvent = new ManualResetEvent(false);
            var connectResetEvent = new ManualResetEvent(false);

            server.MessageReceived += (sender, receivedMessageArgs) =>
            {
                server.SendMessageAsync(receivedMessageArgs.ClientId, receivedMessageArgs.Message).Wait();
            };

            server.ClientConnected += (sender, obj) =>
            {
                Debug.WriteLine("Client connected");
                connectResetEvent.Set();
            };

            client.Disconnected += (sender, obj) =>
            {
                receivedClosingDescription = obj.ClosingStatusDescription;
                disconnectResetEvent.Set();
            };

            client.MessageReceived += (sender, obj) =>
            {
                receivedMessage = obj.Message;
                messageResetEvent.Set();
            };

            // Act
            server.Start();
            await client.ConnectAsync();

            WaitForManualResetEventOrThrow(connectResetEvent);

            await client.SendMessageAsync(Message);
            WaitForManualResetEventOrThrow(messageResetEvent);

            await server.ShutdownServer(CancellationToken.None);
            WaitForManualResetEventOrThrow(disconnectResetEvent, 100);

            _logMessages.ForEach(m => Trace.WriteLine(m));

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
            const int clientsCount = 200;

            object clientConnectLock = new();
            var clientsConnectedCount = 0;

            object receivedMessageLock = new();
            var receivedMessagesCount = 0;

            object clientDisconnectLock = new();
            var clientsDisconnectedCount = 0;

            server.MessageReceived += (sender, receivedMessageArgs) =>
            {
                lock (receivedMessageLock)
                {
                    if (receivedMessageArgs.Message == message)
                    {
                        receivedMessagesCount++;
                    }
                }
            };

            server.ClientConnected += (sender, obj) =>
            {
                lock (receivedMessageLock)
                {
                    clientsConnectedCount++;
                }
            };

            // Act
            server.Start(CancellationToken.None);

            for (int i = 0; i < clientsCount; i++)
            {
                var client = new SimpleWebSocketClient(IPAddress.Loopback.ToString(), 8010, "/");
                client.Disconnected += (sender, obj) =>
                {
                    lock (clientDisconnectLock)
                    {
                        clientsDisconnectedCount++;
                    }
                };
                clients.Add(client);
                await client.ConnectAsync();
            }

            for (int i = 0; i < clientsCount; i++)
            {
                await clients[i].SendMessageAsync(message);
            }

            await server.ShutdownServer(CancellationToken.None);

            await Task.Delay(10);

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