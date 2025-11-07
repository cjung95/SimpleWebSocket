// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket.Exceptions;
using Jung.SimpleWebSocket.Models;
using Jung.SimpleWebSocket.UnitTests.Mock;
using NUnit.Framework;
using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;

// internals of the simple web socket project are visible to the test project
// because of the InternalsVisibleTo attribute in the AssemblyInfo.cs

namespace Jung.SimpleWebSocket.UnitTests
{
    [TestFixture]
    public class SimpleWebSocketTest
    {
        private ILoggerMockHelper<SimpleWebSocketServer> _serverLoggerMockHelper = null!;
        private ILoggerMockHelper<SimpleWebSocketClient> _clientLoggerMockHelper = null!;

        [OneTimeSetUp]
        public void SetUpOnce()
        {
            Trace.Listeners.Add(new ConsoleTraceListener());
        }

        [SetUp]
        public void SetUp()
        {
            _serverLoggerMockHelper = new("Server");
            _clientLoggerMockHelper = new("Client");
        }

        [OneTimeTearDown]
        public void EndTest()
        {
            Trace.Flush();
        }

        [Test]
        public void ChangeClientId_UserIdUnique_ShouldUpdateId()
        {
            // Arrange
            var serverOptions = new SimpleWebSocketServerOptions
            {
                LocalIpAddress = IPAddress.Any,
                Port = 8010,
            };

            var connectedClient1 = new WebSocketServerClient();
            var connectedClient2 = new WebSocketServerClient();
            var oldId = connectedClient1.Id;

            using var server = new SimpleWebSocketServer(serverOptions, _serverLoggerMockHelper.Logger);
            if (!server.ActiveClients.TryAdd(connectedClient1.Id, connectedClient1) ||
            !server.ActiveClients.TryAdd(connectedClient2.Id, connectedClient2))
            {
                throw new Exception("Could not add clients to the server.");
            }

            // Act
            var newId = Guid.NewGuid().ToString();
            server.ChangeClientId(connectedClient1, newId);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(connectedClient1.Id, Is.EqualTo(newId));
                Assert.That(server.ActiveClients.ContainsKey(oldId), Is.False);
                Assert.That(server.ActiveClients.ContainsKey(newId), Is.True);
            });
        }

        [Test]
        public void ChangeClientId_UserIdDuplicated_ShouldThrowException()
        {
            // Arrange
            var serverOptions = new SimpleWebSocketServerOptions
            {
                LocalIpAddress = IPAddress.Any,
                Port = 8010,
            };

            var connectedClient1 = new WebSocketServerClient();
            var connectedClient2 = new WebSocketServerClient();


            using var server = new SimpleWebSocketServer(serverOptions, _serverLoggerMockHelper.Logger);
            if (!server.ActiveClients.TryAdd(connectedClient1.Id, connectedClient1) ||
            !server.ActiveClients.TryAdd(connectedClient2.Id, connectedClient2))
            {
                throw new Exception("Could not add clients to the server.");
            }

            // Act & Assert
            Assert.That(() => server.ChangeClientId(connectedClient1, connectedClient2.Id), Throws.Exception.TypeOf<ClientIdAlreadyExistsException>());
        }

        [Test]
        public void ChangeClientId_TargetUserNotExisting_ShouldThrowException()
        {
            // Arrange
            var serverOptions = new SimpleWebSocketServerOptions
            {
                LocalIpAddress = IPAddress.Any,
                Port = 8010,
            };

            using var server = new SimpleWebSocketServer(serverOptions, _serverLoggerMockHelper.Logger);

            // Act & Assert
            Assert.That(() => server.ChangeClientId(new WebSocketServerClient(), Guid.NewGuid().ToString()), Throws.Exception.TypeOf<ClientNotFoundException>());
        }

        [Test]
        [Platform("Windows7,Windows8,Windows8.1,Windows10", Reason = "This test establishes a TCP serverConnection-server connection using SimpleWebSocket, which relies on specific networking features and behaviors that are only available and consistent on Windows platforms. Running this test on non-Windows platforms could lead to inconsistent results or failures due to differences in networking stack implementations.")]
        public async Task TestClientServerConnection_ShouldSendAndReceiveHelloWorld()
        {
            // Arrange
            var serverOptions = new SimpleWebSocketServerOptions
            {
                LocalIpAddress = IPAddress.Any,
                Port = 8010,
            };

            var clientOptions = new SimpleWebSocketClientOptions
            {
                IPAddress = IPAddress.Loopback,
                Port = 8010,
                RequestPath = "/"
            };

            using var server = new SimpleWebSocketServer(serverOptions, _serverLoggerMockHelper.Logger);
            using var client = new SimpleWebSocketClient(clientOptions, logger: _clientLoggerMockHelper.Logger);


            const string Message = "Hello World";
            const string ClosingStatusDescription = "closing status test description";
            string receivedMessage = string.Empty;
            string receivedClosingDescription = string.Empty;

            var messageResetEvent = new ManualResetEvent(false);
            var disconnectResetEvent = new ManualResetEvent(false);
            var connectResetEvent = new ManualResetEvent(false);

            server.MessageReceived += (sender, receivedMessageArgs) =>
            {
                var serverConnection = server.GetClientById(receivedMessageArgs.ClientId);
                serverConnection.SendMessageAsync(receivedMessageArgs.Message).Wait();
            };

            server.ClientConnected += (sender, obj) =>
            {
                Debug.WriteLine("Client connected");
                connectResetEvent.Set();
            };

            server.ClientDisconnected += (sender, obj) =>
            {
                receivedClosingDescription = obj.ClosingStatusDescription ?? string.Empty;
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
                var IpAddress = (args.Client.RemoteEndPoint as IPEndPoint)?.Address;
                if (IpAddress == null)
                {
                    args.AcceptRequest = false;
                    return;
                }

                // Check the IpAddress against the database
                var isWhitelistedEndPoint = await DbContext_IpAddresses_Contains(IpAddress, cancellationToken);
                if (!isWhitelistedEndPoint)
                {
                    args.ResponseContext.StatusCode = HttpStatusCode.Forbidden;
                    args.ResponseContext.BodyContent = "Connection only possible via local network.";
                    args.AcceptRequest = false;
                }
                args.Client.Properties["test"] = "test";
            };

            // Act
            server.Start();
            await client.ConnectAsync();
            WaitForManualResetEventOrThrow(connectResetEvent);

            await client.SendTextMessageAsync(Message);
            WaitForManualResetEventOrThrow(messageResetEvent);

            await client.DisconnectAsync(ClosingStatusDescription);
            WaitForManualResetEventOrThrow(disconnectResetEvent);

            // test if the server accepts the client again
            var client2 = new SimpleWebSocketClient(clientOptions, logger: _clientLoggerMockHelper.Logger);
            await client2.ConnectAsync();

            await Task.Delay(100);

            await client2.SendTextMessageAsync("Hello World");

            await server.ShutdownServerAsync();
            Array.ForEach(LoggerMessages.GetMessages(), m => Trace.WriteLine(m));

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(receivedMessage, Is.EqualTo(Message));
                Assert.That(receivedClosingDescription, Is.EqualTo(ClosingStatusDescription));
            });
        }

        /// <summary>
        /// Fake Async method to simulate a database call to check if the IP address is in the database.
        /// </summary>
        /// <param name="ipAddress">The IP address to check.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a value indicating whether the IP address is in the database.</returns>
        private static async Task<bool> DbContext_IpAddresses_Contains(IPAddress ipAddress, CancellationToken cancellationToken)
        {
            await Task.Delay(100, cancellationToken);
            return ipAddress.Equals(IPAddress.Loopback);
        }

        [Test]
        [Platform("Windows7,Windows8,Windows8.1,Windows10", Reason = "This test establishes a TCP serverConnection-server connection using SimpleWebSocket, which relies on specific networking features and behaviors that are only available and consistent on Windows platforms. Running this test on non-Windows platforms could lead to inconsistent results or failures due to differences in networking stack implementations.")]
        public async Task TestClientServerConnection_ShouldSendAndReceiveHelloWorld2()
        {
            // Arrange
            var serverOptions = new SimpleWebSocketServerOptions
            {
                LocalIpAddress = IPAddress.Any,
                Port = 8010
            };

            var clientOptions = new SimpleWebSocketClientOptions
            {
                IPAddress = IPAddress.Loopback,
                Port = 8010,
                RequestPath = "/"
            };

            using var server = new SimpleWebSocketServer(serverOptions, _serverLoggerMockHelper.Logger);
            using var client = new SimpleWebSocketClient(clientOptions, logger: _clientLoggerMockHelper.Logger);


            const string Message = "Hello World";
            const string ClosingStatusDescription = "Server is shutting down";
            string receivedMessage = string.Empty;
            string receivedClosingDescription = string.Empty;

            var messageResetEvent = new ManualResetEvent(false);
            var disconnectResetEvent = new ManualResetEvent(false);
            var connectResetEvent = new ManualResetEvent(false);

            server.MessageReceived += (sender, receivedMessageArgs) =>
            {
                var serverConnection = server.GetClientById(receivedMessageArgs.ClientId);
                serverConnection.SendMessageAsync(receivedMessageArgs.Message).Wait();
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

            await client.SendTextMessageAsync(Message);
            WaitForManualResetEventOrThrow(messageResetEvent);

            await server.ShutdownServerAsync();
            WaitForManualResetEventOrThrow(disconnectResetEvent, 100);

            Array.ForEach(LoggerMessages.GetMessages(), m => Trace.WriteLine(m));

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(receivedMessage, Is.EqualTo(Message));
                Assert.That(receivedClosingDescription, Is.EqualTo(ClosingStatusDescription));
            });
        }

        [Test]
        [Platform("Windows7,Windows8,Windows8.1,Windows10", Reason = "This test establishes a TCP serverConnection-server connection using SimpleWebSocket, which relies on specific networking features and behaviors that are only available and consistent on Windows platforms. Running this test on non-Windows platforms could lead to inconsistent results or failures due to differences in networking stack implementations.")]
        public async Task TestMultipleClientServerConnection_ShouldSendAndReceiveHelloWorld()
        {
            // Arrange
            var serverOptions = new SimpleWebSocketServerOptions
            {
                LocalIpAddress = IPAddress.Any,
                Port = 8010
            };

            var clientOptions = new SimpleWebSocketClientOptions
            {
                IPAddress = IPAddress.Loopback,
                Port = 8010,
                RequestPath = "/"
            };

            using var server = new SimpleWebSocketServer(serverOptions);
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
                var client = new SimpleWebSocketClient(clientOptions);
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
                await clients[i].SendTextMessageAsync(message);
            }

            await server.ShutdownServerAsync();

            await Task.Delay(10);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(clientsConnectedCount, Is.EqualTo(clientsCount));
                Assert.That(receivedMessagesCount, Is.EqualTo(clientsCount));
                Assert.That(clientsDisconnectedCount, Is.EqualTo(clientsCount));
            });
        }

        [Test]
        [Platform("Windows7,Windows8,Windows8.1,Windows10", Reason = "This test establishes a TCP serverConnection-server connection using SimpleWebSocket, which relies on specific networking features and behaviors that are only available and consistent on Windows platforms. Running this test on non-Windows platforms could lead to inconsistent results or failures due to differences in networking stack implementations.")]
        public async Task TestServerRestartability_AfterShutdown_ShouldAllowReconnectsAndMessaging()
        {
            // Arrange

            var restartCounter = 0;

            var serverOptions = new SimpleWebSocketServerOptions
            {
                LocalIpAddress = IPAddress.Any,
                Port = 8010
            };

            var clientOptions = new SimpleWebSocketClientOptions
            {
                IPAddress = IPAddress.Loopback,
                Port = 8010
            };

            // Create server and client instances
            using var server = new SimpleWebSocketServer(serverOptions, _serverLoggerMockHelper.Logger);
            using var client = new SimpleWebSocketClient(clientOptions, logger: _clientLoggerMockHelper.Logger);

            // Variables and reset events
            const string Message = "Hello World";
            string receivedMessage = string.Empty;
            var messageResetEvent = new ManualResetEvent(false);
            var disconnectServerResetEvent = new ManualResetEvent(false);
            var disconnectClientResetEvent = new ManualResetEvent(false);
            var connectResetEvent = new ManualResetEvent(false);

            // Event handlers
            server.MessageReceived += (sender, receivedMessageArgs) =>
            {
                var serverConnection = server.GetClientById(receivedMessageArgs.ClientId);
                serverConnection.SendMessageAsync(receivedMessageArgs.Message).Wait();
            };

            server.ClientConnected += (sender, obj) =>
            {
                connectResetEvent.Set();
            };

            server.ClientDisconnected += (sender, obj) =>
            {
                disconnectServerResetEvent.Set();
            };

            client.Disconnected += (sender, obj) =>
            {
                disconnectClientResetEvent.Set();
            };

            client.MessageReceived += (sender, obj) =>
            {
                receivedMessage = obj.Message;
                messageResetEvent.Set();
            };


            // Act
            while (restartCounter++ < 10)
            {
                // Start server and connect client
                server.Start();
                await client.ConnectAsync();
                WaitForManualResetEventOrThrow(connectResetEvent);

                // Send and receive message
                await client.SendTextMessageAsync(Message);
                WaitForManualResetEventOrThrow(messageResetEvent);

                // Alternate between server shutdown and client disconnect
                if (restartCounter % 2 == 0)
                {
                    // Disconnect client and then shutdown server
                    await client.DisconnectAsync();
                    WaitForManualResetEventOrThrow(disconnectServerResetEvent);
                    await server.ShutdownServerAsync();
                }
                else
                {
                    // Shutdown server which will disconnect the client
                    await server.ShutdownServerAsync();
                    WaitForManualResetEventOrThrow(disconnectClientResetEvent);
                }

                // Reset events for next iteration
                connectResetEvent.Reset();
                messageResetEvent.Reset();
                disconnectServerResetEvent.Reset();
                disconnectClientResetEvent.Reset();
            }

            // Log all messages
            Array.ForEach(LoggerMessages.GetMessages(), m => Trace.WriteLine(m));

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(receivedMessage, Is.EqualTo(Message));
            });
        }

        /// <summary>
        /// Throws a TimeoutException if the ManualResetEvent is not set within the specified timeout.
        /// </summary>
        /// <param name="manualResetEvent">The ManualResetEvent to wait for.</param>
        /// <param name="millisecondsTimeout">The timeout in milliseconds. Default is 100 ms.</param>
        /// <param name="resetEventName">The name of the ManualResetEvent parameter. Used for exception message.</param>
        /// <exception cref="TimeoutException">The ManualResetEvent was not set within the timeout period.</exception>
        private static void WaitForManualResetEventOrThrow(ManualResetEvent manualResetEvent, int millisecondsTimeout = 100, [CallerArgumentExpression(nameof(manualResetEvent))] string? resetEventName = null)
        {
            if (!manualResetEvent.WaitOne(millisecondsTimeout))
            {
                throw new TimeoutException($"Timeout waiting for {resetEventName}");
            }
        }
    }
}