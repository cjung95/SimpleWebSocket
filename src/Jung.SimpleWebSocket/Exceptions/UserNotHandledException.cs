// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket.Models;

namespace Jung.SimpleWebSocket.Exceptions
{
    [Serializable]
    internal class UserNotHandledException(WebContext responseContext) : Exception
    {
        public WebContext ResponseContext { get; set; } = responseContext;
    }
}