// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket;
using Jung.SimpleWebSocket.Models;
using Jung.SimpleWebSocket.Models.EventArguments;

namespace BasicUserHandlingClientExample
{
    internal class Program
    {
        /// <summary>
        /// An example of a basic WebSocket client using the Jung.SimpleWebSocket library.
        /// </summary>
        static async Task Main()
        {
            // Create client options
            var clientOptions = new SimpleWebSocketClientOptions
            {
                IPAddress = System.Net.IPAddress.Loopback,
                Port = 8080,
                RequestPath = "/chat"
            };

            // Create the WebSocket client
            using var simpleWebSocketClient = new SimpleWebSocketClient(clientOptions);

            // Subscribe to client events
            simpleWebSocketClient.Disconnected += (s, e) => Console.WriteLine($"Disconnected from the server. Reason: {e.ClosingStatusDescription}");
            simpleWebSocketClient.MessageReceived += (s, e) => Console.WriteLine($"Message received from server: {e.Message}");
            simpleWebSocketClient.BinaryMessageReceived += (s, e) => Console.WriteLine($"Binary message received from server: {BitConverter.ToString(e.Message)}");
            simpleWebSocketClient.SendingUpgradeRequestAsync += SimpleWebSocketClient_SendingUpgradeRequestAsync;

            try
            {
                // Connect to the server
                await simpleWebSocketClient.ConnectAsync();

                // Simulate any delay
                Thread.Sleep(1000);

                // Send a message to the server
                Console.WriteLine("Sending message to the server: Hello, Server!");
                await simpleWebSocketClient.SendTextMessageAsync("Hello, Server!");

                byte[] binaryMessage = [0x01, 0x02, 0x03, 0x04, 0x05];
                Console.WriteLine("Sending binary message to the server: " + BitConverter.ToString(binaryMessage));
                await simpleWebSocketClient.SendBinaryDataAsync(binaryMessage);

                // Keep the server running until a key is pressed
                Console.WriteLine("Press Enter to stop the client...");
                Console.ReadKey();

                // You do not have to explicitly disconnect the client because of the using statement
                // We do it anyway to send the server the closing status description
                await simpleWebSocketClient.DisconnectAsync("Client is shutting down");
            }
            catch (Exception exception)
            {
                var exceptionMessage = exception.Message;
                if (exception.InnerException != null)
                {
                    exceptionMessage += $" Inner exception: {exception.InnerException.Message}";
                }
                Console.WriteLine($"An error occurred: {exceptionMessage}");

                // Keep the console application running until a key is pressed
                Console.WriteLine("Press Enter close this window...");
                Console.ReadKey();
            }
        }

        /// <summary>
        /// Handles the event triggered before a WebSocket upgrade request is sent, allowing customization of the
        /// request.
        /// </summary>
        /// <param name="sender">The source of the event, the WebSocket client instance.</param>
        /// <param name="e">The event arguments containing details about the upgrade request, including headers and other context.</param>
        /// <param name="cancellationToken">The cancellation token of the client.</param>
        /// <returns>A completed task, as this method performs its operation synchronously.</returns>
        private static Task SimpleWebSocketClient_SendingUpgradeRequestAsync(object sender, SendingUpgradeRequestArgs e, CancellationToken cancellationToken)
        {
            // Add a custom header to the upgrade request
            e.WebContext.Headers["User-Name"] = "Alice";

            // Because this is a synchronous method, we return a completed task.
            return Task.CompletedTask;
        }
    }
}
