// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket;
using Jung.SimpleWebSocket.Models;
using Jung.SimpleWebSocket.Models.EventArguments;
using System.Collections.Concurrent;

namespace BasicUserHandlingServerExample
{
    internal class Program
    {
        // A thread-safe dictionary to store connected users
        private static readonly ConcurrentDictionary<string, string> _connectedUsers = [];

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
            simpleWebSocketServer.ClientConnected += SimpleWebSocketServer_ClientConnected;
            simpleWebSocketServer.ClientDisconnected += SimpleWebSocketServer_ClientDisconnected;
            simpleWebSocketServer.MessageReceived += SimpleWebSocketServer_MessageReceived;
            simpleWebSocketServer.BinaryMessageReceived += SimpleWebSocketServer_BinaryMessageReceived;
            simpleWebSocketServer.ClientUpgradeRequestReceivedAsync += SimpleWebSocketServer_ClientUpgradeRequestReceivedAsync;

            // Start the server
            simpleWebSocketServer.Start();
            Console.WriteLine($"Server started on ws://{serverOptions.LocalIpAddress}:{serverOptions.Port}");

            // Keep the server running until a key is pressed
            Console.WriteLine("Press Enter to stop the server...");
            Console.ReadKey();

            // You do not have to explicitly shutdown the server because of the using statement
            // simpleWebSocketServer.ShutdownServer().Wait();
        }

        /// <summary>
        /// This event is triggered when a client successfully connects to the server.
        /// </summary>
        /// <param name="sender">The server that the client connected to.</param>
        /// <param name="e">The event arguments containing the client ID.</param>
        private static void SimpleWebSocketServer_ClientConnected(object? sender, ClientConnectedArgs e)
        {
            if (((SimpleWebSocketServer)sender!).GetClientById(e.ClientId) is WebSocketServerClient client)
            {
                Console.WriteLine($"User name {client.Properties["UserName"]} connected successfully.");
            }
        }

        /// <summary>
        /// This event is triggered when a client disconnects from the server.
        /// </summary>
        /// <param name="sender">The server that the client disconnected from.</param>
        /// <param name="e">The event arguments containing the client and the disconnection details.</param>
        private static void SimpleWebSocketServer_ClientDisconnected(object? sender, ClientDisconnectedArgs e)
        {
            // Remove the user from the connected users list
            var userName = e.Client.Properties["UserName"]?.ToString() ?? "Unknown";
            _connectedUsers.TryRemove(userName, out _);
            Console.WriteLine($"User name {userName} disconnected.");
        }


        /// <summary>
        /// This event is triggered when a text message is received from a client.
        /// </summary>
        /// <param name="sender">The server that received the message.</param>
        /// <param name="e">The event arguments containing the client ID and the message.</param>
        private static void SimpleWebSocketServer_MessageReceived(object? sender, ClientMessageReceivedArgs e)
        {
            if (((SimpleWebSocketServer)sender!).GetClientById(e.ClientId) is WebSocketServerClient client)
            {
                Console.WriteLine($"Message received from {client.Properties["UserName"]}: {e.Message}");
            }
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

            // Get the user name from the request headers
            var userName = e.WebContext.Headers["User-Name"];
            if (userName != null)
            {
                Console.WriteLine($"Request with user name: {userName}");

                // Check if the user name is already connected
                if (_connectedUsers.ContainsKey(userName))
                {
                    Console.WriteLine($"User name {userName} is already connected. Rejecting the upgrade request.");
                    e.ResponseContext.StatusCode = System.Net.HttpStatusCode.Conflict; // 409 Conflict
                    e.ResponseContext.BodyContent = "User name is already connected";
                    e.AcceptRequest = false;
                }
                else
                {
                    // Add the user name to the connected users list
                    _connectedUsers.TryAdd(userName, e.Client.Id);
                    // Store the user name in the client properties for future reference
                    e.Client.Properties["UserName"] = userName;
                }
            }
            else
            {
                // It is recommended to set a status code higher than 400 to reject the upgrade request.
                e.ResponseContext.StatusCode = System.Net.HttpStatusCode.BadRequest; // 400 Bad Request
                e.ResponseContext.BodyContent = "Missing User-Name header";
                // Handle the request and reject it
                e.AcceptRequest = false;
            }

            // Because this is an async event, we need to return a completed task.
            return Task.CompletedTask;
        }
    }
}
