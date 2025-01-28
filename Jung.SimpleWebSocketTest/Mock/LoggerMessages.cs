
namespace Jung.SimpleWebSocketTest.Mock
{
    internal static class LoggerMessages
    {
        private static readonly object _lock = new object();
        internal static List<string> Messages { get; } = [];

        internal static void AddMessage(string message)
        {
            lock (_lock)
            {
                Messages.Add(message);
            }
        }

        internal static string[] GetMessages()
        {
            lock (_lock)
            {
                return [.. Messages];
            }
        }
    }
}
