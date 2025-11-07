// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

namespace Jung.SimpleWebSocket.Models.Messages
{
    internal class BinaryMessageSavedMessage(FileStream fileStream, long length, string tempFilePath, Func<ValueTask> cleanup) : MessageBase
    {
        public FileStream FileStream { get; } = fileStream;
        public long Length { get; } = length;
        public string TempFilePath { get; } = tempFilePath;
        public Func<ValueTask> Cleanup { get; } = cleanup;
    }
}
