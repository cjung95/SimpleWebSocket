// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket.Exceptions;
using Jung.SimpleWebSocket.Models;
using Jung.SimpleWebSocket.Models.EventArguments;
using Microsoft.Extensions.Logging;

namespace Jung.SimpleWebSocket.IntegrationTests.Tests
{
    [TestInformation(Role = "Client", Description = "Stability test - Sends binary messages at random times (between 5s and 20s)")]
    internal class SendBinaryMessagesLoopTest(ILogger<SendBinaryMessagesLoopTest> logger, ILogger<SimpleWebSocketClient> clientLogger) : BaseTest(logger)
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
            Random random = Random.Shared;
            int fileCount = 1;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (!client.IsConnected)
                    {
                        _logger.LogWarning("Client is not connected. Stopping file sending loop.");
                        break;
                    }

                    int sizeMb = random.Next(1, 11); // 1..10 MB
                    long sizeBytes = sizeMb * 1024L * 1024L;

                    // Create a temp file
                    string tempPath = Path.GetTempFileName();

                    // Write random bytes to file asynchronously
                    const int bufferSize = 81920;
                    byte[] buffer = new byte[bufferSize];
                    long remaining = sizeBytes;

                    _logger.LogInformation("Creating file #{count}: {path} ({sizeMb} MB)", fileCount, tempPath, sizeMb);

                    using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, useAsync: true))
                    {
                        while (remaining > 0)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            int toWrite = (int)Math.Min(buffer.Length, remaining);
#if NET6_0_OR_GREATER
                            random.NextBytes(buffer.AsSpan(0, toWrite));
#else
                            // Fallback for older Random implementations
                            if (toWrite == buffer.Length)
                            {
                                random.NextBytes(buffer);
                            }
                            else
                            {
                                var slice = new byte[toWrite];
                                random.NextBytes(slice);
                                Array.Copy(slice, 0, buffer, 0, toWrite);
                            }
#endif
                            await fs.WriteAsync(buffer.AsMemory(0, toWrite), cancellationToken).ConfigureAwait(false);
                            remaining -= toWrite;
                        }

                        await fs.FlushAsync(cancellationToken).ConfigureAwait(false);
                    }

                    _logger.LogInformation("Sending file #{count}: {path} ({sizeMb} MB)", fileCount++, tempPath, sizeMb);

                    // Send the file 
                    await client.SendFileAsync(tempPath, cancellationToken).ConfigureAwait(false);

                    _logger.LogInformation("Sent file: {path}", tempPath);

                    // Try to delete the temp file, log warning on failure
                    try
                    {
                        File.Delete(tempPath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete temp file {path}", tempPath);
                    }

                    await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Graceful exit on cancellation
                    break;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Error while sending the file.");
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
