// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket.Models;
using Jung.SimpleWebSocket.Models.EventArguments;
using Microsoft.Extensions.Logging;

namespace Jung.SimpleWebSocket.IntegrationTests.Tests
{
    [TestInformation(Role = "Server", Description = "Display the events of the server. Stream binary data to file.")]
    internal class DisplayEvents1Test : BaseTest
    {
        /// <summary>
        /// The SimpleWebSocketServer instance.
        /// </summary>
        public SimpleWebSocketServer SimpleWebSocketServer { get; }

        public DisplayEvents1Test(ILogger<DisplayEvents1Test> logger, ILogger<SimpleWebSocketServer> serverLogger)
            : base(logger)
        {
            var serverOptions = new SimpleWebSocketServerOptions()
            {
                LocalIpAddress = System.Net.IPAddress.Any,
                Port = 8085,
                AutoDeleteTempFiles = true,
                MessageProcessingMode = BinaryMessageProcessingMode.StreamBinaryToFile,
            };

            SimpleWebSocketServer = new SimpleWebSocketServer(serverOptions, serverLogger);
        }

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

            await SimpleWebSocketServer.ShutdownServerAsync();
        }

        private void InitializeEventHandlers()
        {
            SimpleWebSocketServer.ClientConnected += SimpleWebSocketServer_ClientConnected;
            SimpleWebSocketServer.ClientDisconnected += SimpleWebSocketServer_ClientDisconnected;
            SimpleWebSocketServer.MessageReceived += SimpleWebSocketServer_MessageReceived;
            SimpleWebSocketServer.BinaryMessageSaved += SimpleWebSocketServer_BinaryMessageSaved;
            SimpleWebSocketServer.ClientUpgradeRequestReceivedAsync += ClientUpgradeRequestReceived;
        }

        private void UnsubscribeEventHandlers()
        {
            SimpleWebSocketServer.ClientConnected -= SimpleWebSocketServer_ClientConnected;
            SimpleWebSocketServer.ClientDisconnected -= SimpleWebSocketServer_ClientDisconnected;
            SimpleWebSocketServer.MessageReceived -= SimpleWebSocketServer_MessageReceived;
            SimpleWebSocketServer.BinaryMessageSaved -= SimpleWebSocketServer_BinaryMessageSaved;
            SimpleWebSocketServer.ClientUpgradeRequestReceivedAsync -= ClientUpgradeRequestReceived;
        }

        private void SimpleWebSocketServer_ClientConnected(object? sender, ClientConnectedArgs e)
        {
            _logger.LogInformation("Client connected: {ClientId}", e.ClientId);
        }

        private void SimpleWebSocketServer_ClientDisconnected(object? sender, ClientDisconnectedArgs e)
        {
            _logger.LogInformation("Client disconnected: {ClientId}", e.Client.Id);
        }

        private void SimpleWebSocketServer_MessageReceived(object? sender, ClientMessageReceivedArgs e)
        {
            _logger.LogInformation("Message received from {ClientId}: {Message}", e.ClientId, e.Message);
        }

        private void SimpleWebSocketServer_BinaryMessageSaved(object? sender, ClientBinaryMessageSavedArgs e)
        {
            _logger.LogInformation("Binary message saved from {ClientId} to temporary file. File size: {fileSize} Bytes", e.ClientId, e.Length);
        }

        private Task ClientUpgradeRequestReceived(object sender, ClientUpgradeRequestReceivedArgs e, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Upgrade request received from {ClientId}.", e.Client.Id);
            return Task.CompletedTask;
        }
    }
}
