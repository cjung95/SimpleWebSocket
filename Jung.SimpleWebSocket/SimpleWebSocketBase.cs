using Jung.SimpleWebSocket.Contracts;
using Microsoft.Extensions.Logging;
using System.Net.WebSockets;
using System.Text;

namespace Jung.SimpleWebSocket
{
    /// <summary>
    /// Represents a base for a WebSocket.
    /// </summary>
    /// <param name="logger">A logger to write internal log messages</param>
    public abstract class SimpleWebSocketBase(ILogger? logger = null) : IWebSocketBase
    {
        /// <inheritdoc/>
        public event Action<object?>? ClientDisconnected;
        /// <inheritdoc/>
        public event Action<string>? MessageReceived;
        /// <inheritdoc/>
        public event Action<byte[]>? BinaryMessageReceived;

        private protected readonly ILogger? _logger = logger;

        private protected async Task ProcessWebSocketMessagesAsync(IWebSocket webSocket, CancellationToken cancellationToken)
        {
            if (webSocket == null)
            {
                throw new InvalidOperationException("WebSocket is not initialized");
            }

            var buffer = new byte[1024 * 4]; // Buffer for incoming data
            while (webSocket.State == WebSocketState.Open)
            {

                // Read the next message
                WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    // Handle the text message
                    string receivedMessage = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    LogInternal("Message received", $"Message received: \"{receivedMessage}\"");
                    _ = Task.Run(() => MessageReceived?.Invoke(receivedMessage), cancellationToken);
                }
                else if (result.MessageType == WebSocketMessageType.Binary)
                {
                    // Handle the binary message
                    LogInternal($"Binary message received", $"Binary message received, length: {result.Count} bytes");
                    _ = Task.Run(() => BinaryMessageReceived?.Invoke(buffer[..result.Count]), cancellationToken);
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    LogInternal("WebSocket closed");
                    _ = Task.Run(() => ClientDisconnected?.Invoke(null), cancellationToken);
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    break;
                }
            }
        }

        private protected void LogInternal(string infoLogMessage, string debugLogMessage = "")
        {
#pragma warning disable CA2254 // Template should be a static expression
            if (!string.IsNullOrEmpty(debugLogMessage) && (_logger?.IsEnabled(LogLevel.Debug) ?? false))
            {
                _logger?.LogDebug(debugLogMessage);
            }
            else
            {
                _logger?.LogInformation(infoLogMessage);
            }
#pragma warning restore CA2254 // Template should be a static expression
        }
    }
}
