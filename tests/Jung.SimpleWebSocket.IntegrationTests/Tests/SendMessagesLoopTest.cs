// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket.Delegates;
using Microsoft.Extensions.Logging;

namespace Jung.SimpleWebSocket.IntegrationTests.Tests
{
    [TestInformation(Role = "Client", Description = "Stability test - Sends messages at random times (between 5s and 20s)")]
    internal class SendMessagesLoopTest(ILogger<SendMessagesLoopTest> logger, ILogger<SimpleWebSocketClient> clientLogger) : BaseTest(logger)
    {
        internal override async Task RunAsync()
        {
            var cancellationTokenSource = new CancellationTokenSource();
            var token = cancellationTokenSource.Token;

            using var client = new SimpleWebSocketClient("localhost", 8085, "", clientLogger);

            InitializeClientEvents(client, cancellationTokenSource);

            try
            {
                await client.ConnectAsync();
            }
            catch (Exception exception)
            {
                _logger.LogError("Failed to connect to the server: {ExceptionMessage}", exception.Message);
                return;
            }

            var task = Task.Run(async () => await SendRandomMessages(client, token));

            Console.WriteLine("Press any key to disconnect from the server...");
            Console.ReadKey();

            await client.DisconnectAsync();
            cancellationTokenSource.Cancel();
            await task;
        }

        private static async Task SendRandomMessages(SimpleWebSocketClient client, CancellationToken cancellationToken)
        {
            Random random = new();
            int messageCount = 1;
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    string message = $"Message {messageCount++} sent at {DateTime.Now}";
                    await client.SendMessageAsync(message, cancellationToken).ConfigureAwait(false);
                    Console.WriteLine($"Sent: {message}");

                    int delay = random.Next(5000, 20001); // Random delay between 5s (5000ms) and 20s (20000ms)
                    await Task.Delay(delay, cancellationToken);
                }
                catch (Exception exception)
                {
                    if (exception is not OperationCanceledException)
                    {
                        Console.WriteLine($"Exception: {exception.Message}");
                    }
                    break;
                }
            }
        }

        private static void InitializeClientEvents(SimpleWebSocketClient client, CancellationTokenSource cancellationTokenSource)
        {
            DisconnectedEventHandler? disconnectedHandler = null;
            MessageReceivedEventHandler? messageReceivedHandler = null;

            disconnectedHandler = (sender, e) =>
            {
                Console.WriteLine("Disconnected");
                cancellationTokenSource.Cancel();
                client.Disconnected -= disconnectedHandler;
                client.MessageReceived -= messageReceivedHandler;
            };

            messageReceivedHandler = (sender, e) =>
            {
                Console.WriteLine($"Message received: {e.Message}");
            };

            client.Disconnected += disconnectedHandler;
            client.MessageReceived += messageReceivedHandler;
        }
    }
}
