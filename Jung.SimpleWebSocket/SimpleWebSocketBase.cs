// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket.Contracts;
using Microsoft.Extensions.Logging;

namespace Jung.SimpleWebSocket
{
    /// <summary>
    /// Represents a base for a WebSocket.
    /// </summary>
    /// <param name="logger">A logger to write internal log messages</param>
    public abstract class SimpleWebSocketBase(ILogger? logger = null) : IWebSocketBase
    {
        private protected readonly ILogger? _logger = logger;

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
