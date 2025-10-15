// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket;
using Jung.SimpleWebSocket.Models.EventArguments;

namespace BasicUserHandlingClientExample
{
    internal class Program
    {
        /// <summary>
        /// An example of a basic WebSocket client using the Jung.SimpleWebSocket library.
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            // Create the WebSocket client and connect to the server at ws://127.0.0.1:8085/chat
            using var simpleWebSocketClient = new SimpleWebSocketClient("127.0.0.1", 8085, "/chat");

            // Subscribe to client events
            simpleWebSocketClient.Disconnected += (s, e) => Console.WriteLine($"Disconnected from the server. Reason: {e.ClosingStatusDescription}");
            simpleWebSocketClient.MessageReceived += (s, e) => Console.WriteLine($"Message received from server: {e.Message}");
            simpleWebSocketClient.BinaryMessageReceived += (s, e) => Console.WriteLine($"Binary message received from server: {BitConverter.ToString(e.Message)}");
            simpleWebSocketClient.SendingUpgradeRequestAsync += SimpleWebSocketClient_SendingUpgradeRequestAsync;

            try
            {
                // Connect to the server
                simpleWebSocketClient.ConnectAsync().GetAwaiter().GetResult();

                // Simulate any delay
                Thread.Sleep(1000);

                // Send a message to the server
                Console.WriteLine("Sending message to the server: Hello, Server!");
                simpleWebSocketClient.SendMessageAsync("Hello, Server!").GetAwaiter().GetResult();

                // Keep the server running until a key is pressed
                Console.WriteLine("Press Enter to stop the server...");
                Console.ReadKey();

                // You do not have to explicitly disconnect the client because of the using statement
                // simpleWebSocketClient.DisconnectAsync("Client is shutting down").GetAwaiter().GetResult();
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

        private static Task SimpleWebSocketClient_SendingUpgradeRequestAsync(object sender, SendingUpgradeRequestArgs e, CancellationToken cancellationToken)
        {
            // Add a custom header to the upgrade request
            e.WebContext.Headers["User-Name"] = "Alice";

            // Because this is a synchronous method, we return a completed task.
            return Task.CompletedTask;

        }
    }
}
