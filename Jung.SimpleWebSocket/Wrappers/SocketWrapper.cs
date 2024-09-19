// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

// This code is based on source code from the System.Net.WebSockets project (https://github.com/dotnet/runtime/tree/main/src/libraries/System.Net.WebSockets)
// Original copyright holder: .NET Foundation
// License: MIT License (https://opensource.org/licenses/MIT)

using Jung.SimpleWebSocket.Contracts;
using Jung.SimpleWebSocket.Exceptions;
using Jung.SimpleWebSocket.Helpers;
using Jung.SimpleWebSocket.Models;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Jung.SimpleWebSocket;

internal partial class SocketWrapper
{
    private const string _supportedVersion = "13";
    private const string _webSocketGUID = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
    private string? _acceptedProtocol;

    private readonly INetworkStream _networkStream;
    private readonly WebSocketHelper _websocketHelper;

    // Regex for a valid request path: must start with a `/` and can include valid path characters.
    [GeneratedRegex(@"^\/[a-zA-Z0-9\-._~\/]*$", RegexOptions.Compiled)]
    private static partial Regex ValidWebSocketPathRegex();
    private static readonly Regex _validPathRegex = ValidWebSocketPathRegex();

    public SocketWrapper(INetworkStream networkStream)
    {
        _networkStream = networkStream;
        _websocketHelper = new WebSocketHelper();
    }

    internal SocketWrapper(INetworkStream networkStream, WebSocketHelper websocketHelper)
    {
        _networkStream = networkStream;
        _websocketHelper = websocketHelper;
    }

    public async Task<WebContext> AwaitContextAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[1024];
        var sb = new StringBuilder();
        var readingStarted = false;

        while (!readingStarted || _networkStream.DataAvailable)
        {
            readingStarted = true;
            var bytesRead = await _networkStream.ReadAsync(buffer, cancellationToken);
            sb.Append(Encoding.ASCII.GetString(buffer[..bytesRead]));
        }

        var request = sb.ToString();
        var context = new WebContext(request);
        return context;
    }

    public async Task AcceptWebSocketAsync(WebContext request, CancellationToken cancellationToken)
    {
        await AcceptWebSocketAsync(request, null, cancellationToken);
    }

    public async Task AcceptWebSocketAsync(WebContext request, string? subProtocol, CancellationToken cancellationToken)
    {
        try
        {
            var response = new WebContext();
            ValidateWebSocketHeaders(request);
            var protocol = request.Headers["Sec-WebSocket-Protocol"];
            if (ProcessWebSocketProtocolHeader(protocol, subProtocol, out var acceptProtocol))
            {
                response.Headers.Add("Sec-WebSocket-Protocol", acceptProtocol);
            }
            var secWebSocketKey = request.Headers["Sec-WebSocket-Key"];
            var secWebSocketAcceptString = ComputeWebSocketAccept(secWebSocketKey!);
            response.Headers.Add("Connection", "Upgrade");
            response.Headers.Add("Upgrade", "websocket");
            response.Headers.Add("Sec-WebSocket-Accept", secWebSocketAcceptString);
            await SendWebSocketResponseHeaders(response, cancellationToken);
            _acceptedProtocol = subProtocol;
        }
        catch (WebSocketServerException)
        {
            throw;
        }
        catch (Exception message)
        {
            throw new WebSocketServerException("Error while accepting the websocket", message);
        }
    }

    private async Task SendWebSocketResponseHeaders(WebContext context, CancellationToken cancellationToken)
    {
        var sb = new StringBuilder(
            $"HTTP/1.1 101 Switching Protocols\r\n");
        AddHeaders(context, sb);
        FinishMessage(sb);

        byte[] responseBytes = Encoding.UTF8.GetBytes(sb.ToString());
        await _networkStream.WriteAsync(responseBytes, cancellationToken);
    }

    private async Task SendWebSocketRequestHeaders(WebContext context, CancellationToken cancellationToken)
    {
        var sb = new StringBuilder(
            $"GET {context.RequestPath} HTTP/1.1\r\n" +
            $"Host: {context.HostName}:{context.Port}\r\n");
        AddHeaders(context, sb);
        FinishMessage(sb);

        byte[] responseBytes = Encoding.UTF8.GetBytes(sb.ToString());
        await _networkStream.WriteAsync(responseBytes, cancellationToken);
    }

    private static void AddHeaders(WebContext response, StringBuilder sb)
    {
        foreach (string header in response.Headers)
        {
            sb.Append($"{header}: {response.Headers[header]}\r\n");
        }
    }

    private static void FinishMessage(StringBuilder sb)
    {
        sb.Append("\r\n");
    }

    private static void ValidateWebSocketHeaders(WebContext context)
    {
        if (!context.IsWebSocketRequest)
        {
            throw new WebSocketServerException("Incoming request is no web socket request");
        }

        if (string.IsNullOrEmpty(context.Headers["Sec-WebSocket-Version"]))
        {
            throw new WebSocketServerException("Missing Sec-WebSocket-Version header");
        }

        if (!string.Equals(context.Headers["Sec-WebSocket-Version"], _supportedVersion, StringComparison.OrdinalIgnoreCase))
        {
            throw new WebSocketServerException("Unsupported Sec-WebSocket-Version");
        }

        var webSocketKey = context.Headers["Sec-WebSocket-Key"];
        if (!string.IsNullOrWhiteSpace(webSocketKey) && Convert.FromBase64String(webSocketKey).Length != 16)
        {
            throw new WebSocketServerException("Invalid Sec-WebSocket-Key");
        }
    }


    internal static bool ProcessWebSocketProtocolHeader(string? clientSecWebSocketProtocol, string? subProtocol, out string acceptProtocol)
    {
        acceptProtocol = string.Empty;
        if (string.IsNullOrEmpty(clientSecWebSocketProtocol))
        {
            // the client has not sent any Sec-WebSocket-Protocol header
            if (subProtocol != null)
            {
                // the server specified a protocol
                throw new WebSocketServerException($"The WebSocket _client did not request any protocols, but server attempted to accept '{subProtocol}' protocol(s).");
            }
            // the server should not send the protocol header
            return false;
        }

        if (subProtocol == null)
        {
            // client send some protocols, server specified 'null'. So server should send headers.
            return true;
        }

        string[] array = clientSecWebSocketProtocol.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        acceptProtocol = subProtocol;
        foreach (string b in array)
        {
            if (string.Equals(acceptProtocol, b, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        throw new WebSocketServerException($"The WebSocket _client requested the following protocols: '{clientSecWebSocketProtocol}', but the server accepted '{subProtocol}' protocol(s).");
    }

    internal async Task SendUpgradeRequestAsync(WebContext requestContext, CancellationToken token)
    {
        // Generate a random Sec-WebSocket-Key
        var secWebSocketKey = Convert.ToBase64String(Guid.NewGuid().ToByteArray());

        ValidateRequestPath(requestContext.RequestPath);
        requestContext.Headers.Add("Upgrade", "websocket");
        requestContext.Headers.Add("Connection", "Upgrade");
        requestContext.Headers.Add("Sec-WebSocket-Key", secWebSocketKey);
        requestContext.Headers.Add("Sec-WebSocket-Version", _supportedVersion);

        await SendWebSocketRequestHeaders(requestContext, token);
    }

    private static void ValidateRequestPath(string requestPath)
    {
        if (!requestPath.StartsWith('/'))
        {
            requestPath = $"/{requestPath}";
        }

        // Check if the path matches the valid path pattern
        if (!_validPathRegex.IsMatch(requestPath))
        {
            throw new WebSocketException("Invalid request path");
        }
    }

    internal static void ValidateUpgradeResponse(WebContext response, WebContext requestContext)
    {
        // Check if the response contains '101 Switching Protocols'
        if (!response.StatusLine.Contains("101 Switching Protocols"))
        {
            throw new WebSocketServerException("Invalid status code, expected '101 Switching Protocols'.");
        }

        // Check for required headers 'Upgrade: websocket' and 'Connection: Upgrade'
        if (!response.ContainsHeader("Upgrade", "websocket") ||
            !response.ContainsHeader("Connection", "Upgrade"))
        {
            throw new WebSocketServerException("Invalid 'Upgrade' or 'Connection' header.");
        }

        // Extract the 'Sec-WebSocket-Accept' value from the server's response
        string? secWebSocketAccept = response.Headers["Sec-WebSocket-Accept"];
        if (string.IsNullOrEmpty(secWebSocketAccept))
        {
            throw new WebSocketServerException("Missing 'Sec-WebSocket-Accept' header.");
        }

        // Generate the expected 'Sec-WebSocket-Accept' value by concatenating the Sec-WebSocket-Key and GUID
        string secWebSocketKey = requestContext.Headers["Sec-WebSocket-Key"]!;
        string expectedAcceptValue = ComputeWebSocketAccept(secWebSocketKey);

        // Compare the computed value with the server's response
        if (secWebSocketAccept != expectedAcceptValue)
        {
            throw new WebSocketServerException("Invalid 'Sec-WebSocket-Accept' value.");
        }
    }

    // Method to compute the expected 'Sec-WebSocket-Accept' value
    private static string ComputeWebSocketAccept(string secWebSocketKey)
    {
        // Concatenate the Sec-WebSocket-Key and the GUID
        string combined = secWebSocketKey + _webSocketGUID;

        // Compute the SHA-1 hash
        byte[] hashBytes = SHA1.HashData(Encoding.UTF8.GetBytes(combined));

        // Convert the hash to a base64 string
        return Convert.ToBase64String(hashBytes);
    }

    /// <summary>
    /// Creates a new <see cref="WebSocket"/> instance from the current <see cref="NetworkStream"/>.
    /// </summary>
    /// <param name="isServer">Web socket should act as server</param>
    /// <param name="keepAliveInterval">The keep alive interval for the connection. Default is 30 seconds.</param>
    /// <returns></returns>
    internal IWebSocket CreateWebSocket(bool isServer, TimeSpan? keepAliveInterval = null)
    {
        keepAliveInterval ??= TimeSpan.FromSeconds(30);
        return _websocketHelper.CreateFromStream(_networkStream.Stream, isServer, _acceptedProtocol, keepAliveInterval.Value);
    }
}