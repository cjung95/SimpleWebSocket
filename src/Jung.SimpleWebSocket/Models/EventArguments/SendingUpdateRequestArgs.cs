// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Microsoft.Extensions.Logging;

namespace Jung.SimpleWebSocket.Models.EventArguments;

/// <summary>
/// Represents the arguments of the event when a upgrade request is sent to a server.
/// </summary>
/// <param name="WebContext">The context of the request.</param>
/// <param name="Logger">The current Logger.</param>
public record SendingUpgradeRequestArgs(WebContext WebContext, ILogger? Logger);
