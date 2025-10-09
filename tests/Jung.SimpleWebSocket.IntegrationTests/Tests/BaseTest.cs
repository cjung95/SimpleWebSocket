// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Microsoft.Extensions.Logging;

namespace Jung.SimpleWebSocket.IntegrationTests.Tests
{
    internal abstract class BaseTest(ILogger logger)
    {
        protected readonly ILogger _logger = logger;

        internal abstract Task RunAsync();
    }
}
