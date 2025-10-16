// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.


namespace Jung.SimpleWebSocket.IntegrationTests.Tests
{
    internal class TestInformationAttribute : Attribute
    {
        public string Role { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}