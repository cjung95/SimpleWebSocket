// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket.Delegates;
using Jung.SimpleWebSocket.Models.EventArguments;

namespace Jung.SimpleWebSocket.Contracts;

/// <summary>
/// Represents A simple WebSocket client.
/// </summary>
public interface IWebSocketClient : IDisposable
{
    /// <summary>
    /// Gets the local ip address of the WebSocket server.
    /// </summary>
    string Host { get; }

    /// <summary>
    /// Gets the port of the WebSocket server.
    /// </summary>
    int Port { get; }

    /// <summary>
    /// Gets the request path of the WebSocket server.
    /// </summary>
    string RequestPath { get; }

    /// <summary>
    /// Gets a value indicating whether the client is connected.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Event that is raised when a message is received from a client.
    /// </summary>
    event EventHandler<MessageReceivedArgs>? MessageReceived;

    /// <summary>
    /// Event that is raised when a binary message is received from a client.
    /// </summary>
    event EventHandler<BinaryMessageReceivedArgs>? BinaryMessageReceived;

    /// <summary>
    /// Event that is raised when a binary message was streamed to disk.
    /// </summary>
    event EventHandler<BinaryMessageSavedArgs>? BinaryMessageSaved;

    /// <summary>
    /// Event that is raised when a client is disconnected.
    /// </summary>
    event EventHandler<DisconnectedArgs>? Disconnected;

    /// <summary>
    /// Occurs before an upgrade request is sent, allowing the request to be inspected or modified asynchronously.
    /// </summary>
    /// <remarks>This event is triggered when an upgrade request is about to be sent. Subscribers can use this
    /// event  to inspect or modify the request by handling the <see cref="SendingUpgradeRequestArgs"/> parameter.  The
    /// event handler is asynchronous, so any modifications or operations should be performed within the  provided
    /// asynchronous context.</remarks>
    event AsyncEventHandler<SendingUpgradeRequestArgs>? SendingUpgradeRequestAsync;

    /// <summary>
    /// Sends a message to all connected clients asynchronously.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SendTextMessageAsync(string message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends binary data to the connected WebSocket server.
    /// </summary>
    /// <param name="data">The binary payload to send.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    Task SendBinaryDataAsync(byte[] data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a file as binary data to the connected WebSocket server.
    /// </summary>
    /// <param name="filePath">The path to the file to send.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SendFileAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts the WebSocket server asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the WebSocket server asynchronously.
    /// </summary>
    /// <param name="closingStatusDescription">The description why the closing status is initiated.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DisconnectAsync(string closingStatusDescription = "Closing", CancellationToken cancellationToken = default);
}
