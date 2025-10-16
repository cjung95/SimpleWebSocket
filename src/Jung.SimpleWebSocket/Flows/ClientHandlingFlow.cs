// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket.Delegates;
using Jung.SimpleWebSocket.Models;
using Jung.SimpleWebSocket.Models.EventArguments;
using Jung.SimpleWebSocket.Utility;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Jung.SimpleWebSocket.Flows
{
    /// <summary>
    /// A flow that handles the client connection.
    /// </summary>
    /// <remarks>
    /// Creates a new instance of the <see cref="ClientHandlingFlow"/> class.
    /// </remarks>
    /// <param name="client">The client to handle.</param>
    /// <param name="server">The server that handles the client.</param>
    /// <param name="cancellationToken">The cancellation token of the server.</param>
    internal class ClientHandlingFlow(SimpleWebSocketServer server, WebSocketServerClient client, CancellationToken cancellationToken)
    {
        /// <summary>
        /// Gets the client associated with the flow.
        /// </summary>
        internal WebSocketServerClient Client { get; set; } = client;

        /// <summary>
        /// Gets the request context of the client.
        /// </summary>
        internal WebContext? Request { get; set; } = null!;

        /// <summary>
        /// Gets the upgrade handler for the client.
        /// </summary>
        private WebSocketUpgradeHandler? _upgradeHandler = null;

        /// <summary>
        /// Gets the response context that is being use to response to the client.
        /// </summary>
        private WebContext? _responseContext = null;

        /// <summary>
        /// Gets the active clients of the server.
        /// </summary>
        private readonly ConcurrentDictionary<string, WebSocketServerClient> _activeClients = server.ActiveClients;

        /// <summary>
        /// Gets the logger of the server.
        /// </summary>
        private readonly ILogger? _logger = server.Logger;

        /// <summary>
        /// Gets the cancellation token of the server.
        /// </summary>
        private readonly CancellationToken _cancellationToken = cancellationToken;

        /// <summary>
        /// Loads the request context.
        /// </summary>
        internal async Task LoadRequestContext()
        {
            var stream = Client.ClientConnection!.GetStream();
            _upgradeHandler = new WebSocketUpgradeHandler(stream);
            Request = await _upgradeHandler.AwaitContextAsync(_cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Accepts the web socket connection.
        /// </summary>
        internal async Task AcceptWebSocketAsync()
        {
            // Check if the response context are initialized
            ThrowForResponseContextNotInitialized(_responseContext);

            // The client is accepted
            await _upgradeHandler!.AcceptWebSocketAsync(Request!, _responseContext, null, _cancellationToken).ConfigureAwait(false);

            // Use the web socket for the client
            Client.UseWebSocket(_upgradeHandler.CreateWebSocket(isServer: true));
            Cleanup();
        }

        /// <summary>
        /// Rejects the web socket connection.
        /// </summary>
        /// <param name="responseContext">The response context to send to the client.</param>
        internal async Task RejectWebSocketAsync(WebContext responseContext)
        {
            // If the status code is SwitchingProtocols, change it to BadRequest
            if (responseContext.StatusCode == System.Net.HttpStatusCode.SwitchingProtocols)
            {
                // If we would send a SwitchingProtocols status code, the client would expect a WebSocket connection.
                // We want to reject the connection, so we send a BadRequest status code.
                // We could get here if the user sets the status code to SwitchingProtocols in the upgrade event.
                responseContext.StatusCode = System.Net.HttpStatusCode.BadRequest;
            }

            // Reject the client
            await _upgradeHandler!.RejectWebSocketAsync(responseContext, _cancellationToken).ConfigureAwait(false);
            Cleanup();
        }

        /// <summary>
        /// Handles the disconnected client.
        /// </summary>
        internal void HandleDisconnectedClient()
        {
            _activeClients.TryRemove(Client.Id, out _);
            Client.Dispose();

            _logger?.LogDebug("Client {clientId} is removed.", Client.Id);
        }

        /// <summary>
        /// Raises the upgrade event.
        /// </summary>
        /// <param name="clientUpgradeRequestReceivedAsync">The event handler for the upgrade request.</param>
        /// <returns>The event arguments of the upgrade request.</returns>
        internal async Task<ClientUpgradeRequestReceivedArgs> RaiseUpgradeEventAsync(AsyncEventHandler<ClientUpgradeRequestReceivedArgs>? clientUpgradeRequestReceivedAsync)
        {
            var eventArgs = new ClientUpgradeRequestReceivedArgs(Client, Request!, _logger);
            await AsyncEventRaiser.RaiseAsync(clientUpgradeRequestReceivedAsync, server, eventArgs, _cancellationToken).ConfigureAwait(false);
            _responseContext = eventArgs.ResponseContext;
            return eventArgs;
        }

        /// <summary>
        /// Tries to add the client to the active user list.
        /// </summary>
        /// <returns>True if the client was added to the active user list. False if the client is already connected.</returns>
        internal bool TryAddClientToActiveUserList()
        {
            return _activeClients.TryAdd(Client.Id, Client);
        }

        /// <summary>
        /// Throws an exception if the response context is not initialized.
        /// </summary>
        /// <param name="responseContext">The response context to check.</param>
        /// <exception cref="InvalidOperationException"></exception>
        private static void ThrowForResponseContextNotInitialized([NotNull] WebContext? responseContext)
        {
            if (responseContext is null)
            {
                throw new InvalidOperationException("The response context is not initialized.");
            }
        }

        /// <summary>
        /// Releases resources that are no longer required.
        /// </summary>
        private void Cleanup()
        {
            _upgradeHandler = null;
            _responseContext = null;
            Request = null;
        }
    }
}
