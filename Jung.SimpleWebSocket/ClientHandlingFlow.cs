// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Net;

namespace Jung.SimpleWebSocket
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
        internal WebContext Request { get; set; } = null!;

        /// <summary>
        /// Gets the upgrade handler for the client.
        /// </summary>
        private WebSocketUpgradeHandler _upgradeHandler = null!;

        /// <summary>
        /// Gets the options of the server.
        /// </summary>
        private readonly SimpleWebSocketServerOptions _options = server.Options;

        /// <summary>
        /// Gets the active clients of the server.
        /// </summary>
        private readonly ConcurrentDictionary<string, WebSocketServerClient> _activeClients = server.ActiveClients;

        /// <summary>
        /// Gets the passive clients of the server.
        /// </summary>
        private readonly IDictionary<string, WebSocketServerClient> _passiveClients = server.PassiveClients;

        /// <summary>
        /// Gets the logger of the server.
        /// </summary>
        private readonly ILogger? _logger = server.Logger;

        /// <summary>
        /// Gets the cancellation token of the server.
        /// </summary>
        private readonly CancellationToken _cancellationToken = cancellationToken;

        /// <summary>
        /// The lock object for the client dictionaries.
        /// </summary>
        private static readonly object _clientLock = new();

        /// <summary>
        /// Loads the request context.
        /// </summary>
        internal async Task LoadRequestContext()
        {
            var stream = Client.ClientConnection!.GetStream();
            _upgradeHandler = new WebSocketUpgradeHandler(stream);
            Request = await _upgradeHandler.AwaitContextAsync(_cancellationToken);
        }

        /// <summary>
        /// Handles the client identification.
        /// </summary>
        internal void HandleClientIdentification()
        {
            // Check if disconnected clients are remembered and can be reactivated
            if (_options.RememberDisconnectedClients)
            {
                // Check if the request contains a user id
                if (Request.ContainsUserId)
                {
                    _logger?.LogDebug("User id found in request: {userId}", Request.UserId);

                    lock (_clientLock)
                    {
                        ThrowForUserAlreadyConnected();

                        // Check if the client is an existing passive client
                        var clientExists = _passiveClients.ContainsKey(Request.UserId);
                        if (clientExists)
                        {
                            _logger?.LogDebug("Passive Client found for user id {userId} - reactivating user.", Request.UserId);

                            // Use the existing client
                            // Update the client with the new connection
                            // Remove the client from the passive clients
                            var passiveClient = _passiveClients[Request.UserId];
                            passiveClient.UpdateClient(Client.ClientConnection!);
                            Client = passiveClient;
                            _passiveClients.Remove(Request.UserId);
                        }
                        else
                        {
                            // Client is not a passive client
                            // Update the clients user id
                            Client.UpdateId(Request.UserId);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Throws an exception if the user is already connected.
        /// </summary>
        /// <exception cref="UserNotHandledException"></exception>
        private void ThrowForUserAlreadyConnected()
        {
            // No passive client found, checking for active clients with the same id
            if (_activeClients.ContainsKey(Request.UserId))
            {
                _logger?.LogDebug("Active Client found for user id {userId} - rejecting connection.", Request.UserId);
                // Reject the connection

                var responseContext = new WebContext
                {
                    StatusCode = HttpStatusCode.Conflict,
                    BodyContent = "User id already in use"
                };
                throw new UserNotHandledException(responseContext);
            }
        }

        /// <summary>
        /// Accepts the websocket connection.
        /// </summary>
        /// <param name="responseContext">The response context to send to the client.</param>
        internal async Task AcceptWebSocketAsync(WebContext responseContext)
        {
            // The client is accepted
            await _upgradeHandler.AcceptWebSocketAsync(Request, responseContext, Client.Id, null, _cancellationToken);

            // Use the websocket for the client
            Client.UseWebSocket(_upgradeHandler.CreateWebSocket(isServer: true));
        }

        /// <summary>
        /// Rejects the websocket connection.
        /// </summary>
        /// <param name="responseContext">The response context to send to the client.</param>
        internal async Task RejectWebSocketAsync(WebContext responseContext)
        {
            await _upgradeHandler.RejectWebSocketAsync(responseContext, _cancellationToken);
        }

        /// <summary>
        /// Handles the disconnected client.
        /// </summary>
        internal void HandleDisconnectedClient()
        {
            lock (_clientLock)
            {
                _activeClients.TryRemove(Client.Id, out _);
                Client.Dispose();

                if (_options.RememberDisconnectedClients)
                {
                    _logger?.LogDebug("Client {clientId} is now a passive user.", Client.Id);
                    _passiveClients.Add(Client.Id, Client);
                }
                else
                {
                    _logger?.LogDebug("Client {clientId} is removed.", Client.Id);
                }
            }
        }
    }
}