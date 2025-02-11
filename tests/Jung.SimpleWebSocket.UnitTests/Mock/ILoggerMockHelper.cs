// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Microsoft.Extensions.Logging;
using Moq;

namespace Jung.SimpleWebSocket.UnitTests.Mock
{
    internal class ILoggerMockHelper<T> where T : class
    {
        internal Mock<ILogger<T>> LoggerMock { get; }
        internal ILogger<T> Logger => LoggerMock.Object;

        public ILoggerMockHelper(string name)
        {
            LoggerMock = new Mock<ILogger<T>>();
            ILoggerMockHelper<T>.SetUpLogger(LoggerMock, name);
        }

        private static void SetUpLogger(Mock<ILogger<T>> mock, string loggerName)
        {
            mock.Setup(m => m.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()!
            )).Callback(new InvocationAction(invocation =>
            {
                var logLevel = (LogLevel)invocation.Arguments[0];
                var eventId = (EventId)invocation.Arguments[1];
                var state = invocation.Arguments[2];
                var exception = (Exception)invocation.Arguments[3];
                var formatter = invocation.Arguments[4];

                var invokeMethod = formatter.GetType().GetMethod("Invoke");
                var logMessage = invokeMethod!.Invoke(formatter, [state, exception]);
                LoggerMessages.AddMessage($"[{DateTime.Now:HH:mm:ss:fff}] {loggerName} ({logLevel}): {logMessage}");
            }));
        }
    }
}
