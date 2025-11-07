// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket.Contracts;

namespace Jung.SimpleWebSocket.Models
{
    /// <summary>
    /// 
    /// </summary>
    public abstract class SimpleWebSocketBaseOptions
    {
        /// <summary>
        /// Which message processing implementation to use.
        /// </summary>
        /// <remarks>
        /// Depending on your choice here, a different event is raised when a full binary message is received:
        /// <list type="bullet">
        /// <item> <see cref="BinaryMessageProcessingMode.InMemory"/>: <see cref="IWebSocketServer.BinaryMessageReceived"/></item>
        /// <item> <see cref="BinaryMessageProcessingMode.StreamBinaryToFile"/>: <see cref="IWebSocketServer.BinaryMessageSaved"/></item>
        /// </list>
        /// </remarks>
        public BinaryMessageProcessingMode MessageProcessingMode { get; set; } = BinaryMessageProcessingMode.InMemory;

        /// <summary>
        /// Chunk size in byte used for the socket read buffer. Default 4KB.
        /// </summary>
        public int ChunkSize { get; set; } = 4 * 1024;

        /// <summary>
        /// Maximum bytes allowed for any single message (in-memory &amp; stream guard).
        /// Tune according to your environment to avoid DoS / OOM. Default 50MB.
        /// </summary>
        public long MaxMessageBytes { get; set; } = 50L * 1024 * 1024;

        /// <summary>
        /// Maximum bytes allowed for text messages in streaming mode. Default 10MB.
        /// </summary>
        public long MaxTextBytes { get; set; } = 10L * 1024 * 1024;

        /// <summary>
        /// Initial capacity used when accumulating binary messages in memory. Default 8KB.
        /// </summary>
        /// <remarks>
        /// Relevant only when <see cref="MessageProcessingMode"/> is <see cref="BinaryMessageProcessingMode.InMemory"/>.
        /// </remarks>
        public int InMemoryInitialBuffer { get; set; } = 8 * 1024;

        /// <summary>
        /// FileStream internal buffer size in byte when streaming binary messages to disk. Default 4KB.
        /// </summary>
        /// <remarks>
        /// Relevant only when <see cref="MessageProcessingMode"/> is <see cref="BinaryMessageProcessingMode.StreamBinaryToFile"/>.
        /// </remarks>
        public int FileStreamBufferSize { get; set; } = 4 * 1024;

        /// <summary>
        /// If true, temp files created for streamed binary messages will be created with DeleteOnClose.
        /// </summary>
        /// <remarks>
        /// Relevant only when <see cref="MessageProcessingMode"/> is <see cref="BinaryMessageProcessingMode.StreamBinaryToFile"/>.
        /// </remarks>
        public bool AutoDeleteTempFiles { get; set; } = false;

        /// <summary>
        /// The path where temp files for streamed binary messages are created. Defaults to system temp path.
        /// </summary>
        /// <remarks>
        /// Relevant only when <see cref="MessageProcessingMode"/> is <see cref="BinaryMessageProcessingMode.StreamBinaryToFile"/>.
        /// </remarks>
        public string TempFilesPath { get; set; } = Path.GetTempPath();

        /// <summary>
        /// The size of the buffer used when sending files. Defaults to 8192 bytes.
        /// </summary>
        public int SendFileBufferSize { get; set; } = 8192;
    }
}
