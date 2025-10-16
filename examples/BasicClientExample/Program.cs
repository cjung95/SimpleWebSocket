// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket;

namespace BasicClientExample
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
                // simpleWebSocketClient.DisconnectAsync("Client is shutting down").Wait();
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
    }
}
