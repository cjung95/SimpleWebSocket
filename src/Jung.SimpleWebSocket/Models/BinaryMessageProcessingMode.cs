// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

namespace Jung.SimpleWebSocket.Models
{
    /// <summary>
    /// Modes for processing incoming WebSocket messages.
    /// </summary>
    public enum BinaryMessageProcessingMode
    {
        /// <summary>Accumulate message in memory (pooled buffers).</summary>
        InMemory = 0,

        /// <summary>Stream binary messages to disk (temp file) to avoid large in-memory allocations.</summary>
        StreamBinaryToFile = 1
    }
}