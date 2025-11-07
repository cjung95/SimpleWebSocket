// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket;
using Jung.SimpleWebSocket.Models;

namespace LargeFileTransferClientExample
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

            try
            {
                // Connect to the server
                await simpleWebSocketClient.ConnectAsync();
                Console.WriteLine("Client connected to the server.");

                // Simulate any delay
                Thread.Sleep(1000);

                // Send a message to the server
                Console.WriteLine("Sending large file to the server...");
                await simpleWebSocketClient.SendFileAsync(@"C:\path\to\large\file.dat");

                Console.WriteLine("File sent, now closing the client.");

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
    }
}
