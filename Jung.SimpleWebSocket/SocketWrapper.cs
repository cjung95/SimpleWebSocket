using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;

namespace Jung.SimpleWebSocket;

internal class SocketWrapper(NetworkStream networkStream)
{
    public static string SupportedVersion = "13";
    private readonly NetworkStream _networkStream = networkStream;

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

    public async Task<WebSocket> AcceptWebSocketAsync(WebContext request, CancellationToken cancellationToken)
    {
        return await AcceptWebSocketAsync(request, null, cancellationToken);
    }

    public async Task<WebSocket> AcceptWebSocketAsync(WebContext request, string? subProtocol, CancellationToken cancellationToken)
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
            var secWebSocketAcceptString = GetSecWebSocketAcceptString(secWebSocketKey!);
            response.Headers.Add("Connection", "Upgrade");
            response.Headers.Add("Upgrade", "websocket");
            response.Headers.Add("Sec-WebSocket-Accept", secWebSocketAcceptString);
            await SendWebSocketResponseHeaders(response, cancellationToken);

            return WebSocket.CreateFromStream(_networkStream, true, protocol, TimeSpan.FromSeconds(30));
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
        var sb = new StringBuilder($"HTTP/1.1 101 Switching Protocols\r\n");
        AddHeaders(context, sb);
        FinishMessage(sb);

        byte[] responseBytes = Encoding.UTF8.GetBytes(sb.ToString());
        await _networkStream.WriteAsync(responseBytes, cancellationToken);
    }

    private void AddHeaders(WebContext response, StringBuilder sb)
    {
        foreach (string header in response.Headers)
        {
            sb.Append($"{header}: {response.Headers[header]}\r\n");
        }
    }

    private void FinishMessage(StringBuilder sb)
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

        if (!string.Equals(context.Headers["Sec-WebSocket-Version"], SupportedVersion, StringComparison.OrdinalIgnoreCase))
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
            if (subProtocol != null)
            {
                throw new WebSocketServerException($"The WebSocket _client did not request any protocols, but server attempted to accept '{subProtocol}' protocol(s).");
            }
            return false;
        }
        if (subProtocol == null)
        {
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

    internal static string GetSecWebSocketAcceptString(string secWebSocketKey)
    {
        string s = secWebSocketKey + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
        byte[] bytes = Encoding.UTF8.GetBytes(s);
        byte[] inArray = SHA1.HashData(bytes);
        return Convert.ToBase64String(inArray);
    }
}
