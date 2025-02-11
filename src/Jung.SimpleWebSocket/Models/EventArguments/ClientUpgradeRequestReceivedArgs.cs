// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Microsoft.Extensions.Logging;

namespace Jung.SimpleWebSocket.Models.EventArguments;

/// <summary>
/// Represents the arguments of the event when a upgrade request is received from a client.
/// </summary>
/// <param name="Client">The client that is sending the upgrade request.</param>
/// <param name="WebContext">The context of the request.</param>
/// <param name="Logger">The current Logger.</param>
public record ClientUpgradeRequestReceivedArgs(WebSocketServerClient Client, WebContext WebContext, ILogger? Logger)
{
    private WebContext? _responseContext;

    /// <summary>
    /// Gets or sets a value indicating whether the upgrade request should be handled.
    /// </summary>
    public bool Handle { get; set; } = true;

    /// <summary>
    /// The context that is being use to response to the client.
    /// </summary>
    public WebContext ResponseContext { get => _responseContext ??= new WebContext(); }
}
