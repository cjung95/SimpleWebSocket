// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket.Exceptions;
using Jung.SimpleWebSocket.Models;
using Jung.SimpleWebSocket.Models.EventArguments;
using Microsoft.Extensions.Logging;

namespace Jung.SimpleWebSocket.IntegrationTests.Tests
{
    [TestInformation(Role = "Client", Description = "Stability test - Sends text messages at random times (between 5s and 20s)")]
    internal class SendTextMessagesLoopTest(ILogger<SendTextMessagesLoopTest> logger, ILogger<SimpleWebSocketClient> clientLogger) : BaseTest(logger)
    {
        internal override async Task RunAsync()
        {
            var cancellationTokenSource = new CancellationTokenSource();
            var token = cancellationTokenSource.Token;

            // Create client options
            var clientOptions = new SimpleWebSocketClientOptions
            {
                IPAddress = System.Net.IPAddress.Loopback,
                Port = 8085,
                RequestPath = string.Empty
            };

            // Create the WebSocket client
            using var client = new SimpleWebSocketClient(clientOptions, clientLogger);

            InitializeClientEvents(client);

            try
            {
                await client.ConnectAsync();

                var task = Task.Run(async () => await SendRandomMessages(client, token));

                Console.WriteLine("Press any key to disconnect from the server...");
                Console.ReadKey();

                await client.DisconnectAsync();
                cancellationTokenSource.Cancel();
                await task;
            }
            catch (WebSocketConnectionException exception)
            {
                _logger.LogError("Failed to connect to the server: {ExceptionMessage}", exception.Message);

            }
            catch (Exception exception)
            {
                _logger.LogError("An error occurred: {ExceptionMessage}", exception.Message);
                return;
            }
            finally
            {
                UnsubscribeEvents(client);
            }
        }

        private async Task SendRandomMessages(SimpleWebSocketClient client, CancellationToken cancellationToken)
        {
            Random random = new();
            int messageCount = 1;
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (!client.IsConnected)
                    {
                        // If the client is not connected, exit the loop.
                        _logger.LogWarning("Client is not connected. Stopping message sending loop.");
                        break;
                    }

                    string message = $"Message {messageCount++} sent at {DateTime.Now}";
                    await client.SendTextMessageAsync(message, cancellationToken).ConfigureAwait(false);
                    _logger.LogInformation("Sent: {message}", message);

                    int delay = random.Next(5000, 20001); // Random delay between 5s (5000ms) and 20s (20000ms)
                    await Task.Delay(delay, cancellationToken);
                }
                catch (Exception exception)
                {
                    if (exception is not OperationCanceledException)
                    {
                        _logger.LogError(exception, "Error while sending the message.");
                    }
                    break;
                }
            }
        }

        private void InitializeClientEvents(SimpleWebSocketClient client)
        {
            client.Disconnected += Client_Disconnected;
            client.MessageReceived += Client_MessageReceived;
            client.BinaryMessageReceived += Client_BinaryMessageReceived;
        }

        private void UnsubscribeEvents(SimpleWebSocketClient client)
        {
            client.Disconnected -= Client_Disconnected;
            client.MessageReceived -= Client_MessageReceived;
            client.BinaryMessageReceived -= Client_BinaryMessageReceived;
        }


        private void Client_BinaryMessageReceived(object? sender, BinaryMessageReceivedArgs e)
        {
            _logger.LogInformation("Binary message received: {binaryMessage}", BitConverter.ToString(e.Message));
        }

        private void Client_MessageReceived(object? sender, MessageReceivedArgs e)
        {
            _logger.LogInformation("Message received: {message}", e.Message);
        }

        private void Client_Disconnected(object? sender, DisconnectedArgs e)
        {
            _logger.LogInformation("Disconnected");
        }
    }
}
