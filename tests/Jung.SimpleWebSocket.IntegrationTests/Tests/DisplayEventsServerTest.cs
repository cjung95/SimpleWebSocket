// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket.Models.EventArguments;
using Microsoft.Extensions.Logging;

namespace Jung.SimpleWebSocket.IntegrationTests.Tests
{
    [TestInformation(Role = "Server", Description = "Display the events of the server.")]
    internal class DisplayEventsTest(SimpleWebSocketServer simpleWebSocketServer, ILogger<DisplayEventsTest> logger) : BaseTest(logger)
    {
        /// <summary>
        /// The SimpleWebSocketServer instance.
        /// </summary>
        public SimpleWebSocketServer SimpleWebSocketServer { get; } = simpleWebSocketServer;

        /// <summary>
        /// Runs the server instance.
        /// </summary>
        internal override async Task RunAsync()
        {
            InitializeEventHandlers();

            SimpleWebSocketServer.Start();

            Console.WriteLine("Press any key to stop the SimpleWebSocketServer...");
            Console.ReadKey();

            UnsubscribeEventHandlers();

            await SimpleWebSocketServer.ShutdownServer();
        }

        private void InitializeEventHandlers()
        {
            SimpleWebSocketServer.ClientConnected += SimpleWebSocketServer_ClientConnected;
            SimpleWebSocketServer.ClientDisconnected += SimpleWebSocketServer_ClientDisconnected;
            SimpleWebSocketServer.MessageReceived += SimpleWebSocketServer_MessageReceived;
            SimpleWebSocketServer.BinaryMessageReceived += SimpleWebSocketServer_BinaryMessageReceived;
            SimpleWebSocketServer.ClientUpgradeRequestReceivedAsync += ClientUpgradeRequestReceived;
        }

        private void UnsubscribeEventHandlers()
        {
            SimpleWebSocketServer.ClientConnected -= SimpleWebSocketServer_ClientConnected;
            SimpleWebSocketServer.ClientDisconnected -= SimpleWebSocketServer_ClientDisconnected;
            SimpleWebSocketServer.MessageReceived -= SimpleWebSocketServer_MessageReceived;
            SimpleWebSocketServer.BinaryMessageReceived -= SimpleWebSocketServer_BinaryMessageReceived;
            SimpleWebSocketServer.ClientUpgradeRequestReceivedAsync -= ClientUpgradeRequestReceived;
        }

        private void SimpleWebSocketServer_ClientConnected(object? sender, ClientConnectedArgs e)
        {
            Console.WriteLine($"Client connected: {e.ClientId}");
        }

        private void SimpleWebSocketServer_ClientDisconnected(object? sender, ClientDisconnectedArgs e)
        {
            Console.WriteLine($"Client disconnected: {e.ClientId}");
        }

        private void SimpleWebSocketServer_MessageReceived(object? sender, ClientMessageReceivedArgs e)
        {
            Console.WriteLine($"Message received from {e.ClientId}: {e.Message}");
        }

        private void SimpleWebSocketServer_BinaryMessageReceived(object? sender, ClientBinaryMessageReceivedArgs e)
        {
            Console.WriteLine($"Binary message received from {e.ClientId}: {string.Join(' ', e.Message)}");
        }

        private static async Task ClientUpgradeRequestReceived(object sender, ClientUpgradeRequestReceivedArgs e, CancellationToken cancellationToken)
        {
            Console.WriteLine($"Upgrade request received from {e.Client.Id}.");
            await Task.CompletedTask;
        }
    }
}
