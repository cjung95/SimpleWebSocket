// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket;
using Jung.SimpleWebSocket.Models;
using Jung.SimpleWebSocket.Models.EventArguments;

namespace BasicServerExample
{
    internal class Program
    {
        /// <summary>
        /// An example of a basic WebSocket server using the Jung.SimpleWebSocket library.
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            // Create server options
            var serverOptions = new SimpleWebSocketServerOptions()
            {
                // Set the server to listen on port 8080 and localhost
                Port = 8085,
                LocalIpAddress = new System.Net.IPAddress([127, 0, 0, 1])
            };

            // Create the WebSocket server
            using var simpleWebSocketServer = new SimpleWebSocketServer(serverOptions);

            // Subscribe to the server events
            simpleWebSocketServer.ClientConnected += (s, e) => Console.WriteLine($"Client connected: {e.ClientId}");
            simpleWebSocketServer.ClientDisconnected += (s, e) => Console.WriteLine($"Client disconnected: {e.Client.Id}, Reason: {e.ClosingStatusDescription}");
            simpleWebSocketServer.MessageReceived += (s, e) => Console.WriteLine($"Message received from {e.ClientId}: {e.Message}");
            simpleWebSocketServer.BinaryMessageReceived += SimpleWebSocketServer_BinaryMessageReceived;
            simpleWebSocketServer.ClientUpgradeRequestReceivedAsync += SimpleWebSocketServer_ClientUpgradeRequestReceivedAsync;

            // Start the server
            simpleWebSocketServer.Start();
            Console.WriteLine("Server started on ws://127.0.0.1:8085");

            // Keep the server running until a key is pressed
            Console.WriteLine("Press Enter to stop the server...");
            Console.ReadKey();

            // You do not have to explicitly shutdown the server because of the using statement
            // simpleWebSocketServer.ShutdownServer().Wait();
        }

        /// <summary>
        /// This event is triggered when a client sends a binary message to the server.
        /// </summary>
        /// <param name="sender">The server that received the binary message.</param>
        /// <param name="e">The event arguments containing the client ID and the binary message.</param>
        private static void SimpleWebSocketServer_BinaryMessageReceived(object? sender, ClientBinaryMessageReceivedArgs e)
        {
            // Convert the binary message to a hex string
            string hex = BitConverter.ToString(e.Message);
            Console.WriteLine($"Binary message received from {e.ClientId}: {hex}");
        }

        /// <summary>
        /// This event is triggered when a client sends an upgrade request to the server.
        /// </summary>
        /// <param name="sender">The server that received the upgrade request.</param>
        /// <param name="e">The event arguments containing the client and the request details.</param>
        /// <param name="cancellationToken">The cancellation token of the server.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private static Task SimpleWebSocketServer_ClientUpgradeRequestReceivedAsync(object sender, ClientUpgradeRequestReceivedArgs e, CancellationToken cancellationToken)
        {
            Console.WriteLine($"Upgrade request received from {e.Client.Id} for {e.WebContext.RequestPath}");

            // Do something with the upgrade request
            // For example save request path to client properties
            e.Client.Properties["RequestPath"] = e.WebContext.RequestPath;

            // Or reject the upgrade request if the request path is not /chat
            if (e.WebContext.RequestPath != "/chat")
            {
                // It is recommended to set a status code higher than 400 to reject the upgrade request.
                e.ResponseContext.StatusCode = System.Net.HttpStatusCode.Forbidden; // 403 Forbidden
                // Handle the request and reject it
                e.AcceptRequest = false;
            }
            else
            {
                // Handle the request and accept it
                e.AcceptRequest = true;
            }

            // Because this is an async event, we need to return a completed task.
            return Task.CompletedTask;
        }
    }
}
