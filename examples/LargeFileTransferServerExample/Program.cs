// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket;
using Jung.SimpleWebSocket.Models;
using Jung.SimpleWebSocket.Models.EventArguments;

namespace LargeFileTransferServerExample
{
    internal class Program
    {
        /// <summary>
        /// An example of a basic WebSocket server using the Jung.SimpleWebSocket library.
        /// </summary>
        static void Main()
        {
            // Create server options
            var serverOptions = new SimpleWebSocketServerOptions()
            {
                // Set the server to listen on port 8080 and localhost
                Port = 8080,
                LocalIpAddress = new System.Net.IPAddress([0, 0, 0, 0]),

                // Configure the server to stream binary messages to temporary files
                MessageProcessingMode = BinaryMessageProcessingMode.StreamBinaryToFile,
                TempFilesPath = Path.GetTempPath(),

                // Delete temporary files automatically after processing
                AutoDeleteTempFiles = true,

                // set buffer sizes and max message sizes
                ChunkSize = 4 * 1024, // 4 KB
                FileStreamBufferSize = 4 * 1024, // 4 KB
                MaxMessageBytes = 10L * 1024 * 1024 * 1024, // 10 GB
            };

            // Create the WebSocket server
            using var simpleWebSocketServer = new SimpleWebSocketServer(serverOptions);

            // Subscribe to the server events
            simpleWebSocketServer.ClientConnected += (s, e) => Console.WriteLine($"Client connected: {e.ClientId}");
            simpleWebSocketServer.ClientDisconnected += (s, e) => Console.WriteLine($"Client disconnected: {e.Client.Id}, Reason: {e.ClosingStatusDescription}");
            simpleWebSocketServer.MessageReceived += (s, e) => Console.WriteLine($"Message received from {e.ClientId}: {e.Message}");
            simpleWebSocketServer.BinaryMessageSaved += SimpleWebSocketServer_BinaryMessageSaved;
            simpleWebSocketServer.ClientUpgradeRequestReceivedAsync += SimpleWebSocketServer_ClientUpgradeRequestReceivedAsync;

            // Start the server
            simpleWebSocketServer.Start();
            Console.WriteLine("Server started, now listening for clients...");

            // Keep the server running until a key is pressed
            Console.WriteLine("Press Enter to stop the server...");
            Console.ReadKey();

            // You do not have to explicitly shutdown the server because of the using statement
            // simpleWebSocketServer.ShutdownServerAsync().Wait();
        }

        /// <summary>
        /// Handles the event triggered when a binary message is saved by the WebSocket server.
        /// </summary>
        /// <remarks>This method processes the binary message stream by copying it to a specified file
        /// location. After processing, it ensures that the server-side cleanup is performed by calling <see
        /// cref="ClientBinaryMessageSavedArgs.CompleteProcessingAsync"/>. If an exception occurs during processing, the
        /// cleanup method is still invoked to maintain server consistency.</remarks>
        /// <param name="sender">The source of the event. This parameter is optional and may be <see langword="null"/>.</param>
        /// <param name="e">An instance of <see cref="ClientBinaryMessageSavedArgs"/> containing the binary message stream and methods
        /// for processing completion.</param>
        private static async void SimpleWebSocketServer_BinaryMessageSaved(object? sender, ClientBinaryMessageSavedArgs e)
        {
            try
            {
                // consume the stream (copy to somewhere, process it, etc.)
                var destinationPath = Path.Combine(Path.GetTempPath(), "copy.temp");
                using var destination = File.Create(destinationPath);
                await e.Stream.CopyToAsync(destination);
                await destination.FlushAsync();

                // Alternatively, you can process the stream directly without saving it to another file.
                // Or you use the saved file directly by using e.TempFilePath.

                // When finished call this to tell server it can clean up
                // If the option AutoDeleteTempFiles in the server options is set to true, the temp file is deleted now.
                await e.CompleteProcessingAsync();
            }
            catch
            {
                // still call CompleteProcessingAsync to ensure server-side cleanup occurs
                await e.CompleteProcessingAsync();
                throw;
            }
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
