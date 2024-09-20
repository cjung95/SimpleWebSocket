// This file is part of the Jung SimpleWebSocket project.
// The project is licensed under the MIT license.

using Jung.SimpleWebSocket.Exceptions;
using System.Collections.Specialized;
using System.Net.Http.Headers;

namespace Jung.SimpleWebSocket.Models;


/// <summary>
/// Represents the context of a web request.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="WebContext"/> class.
/// </remarks>
/// <param name="content">The content of the web request.</param>
internal class WebContext(string? content = null)
{

    /// <summary>
    /// The content of the web request.
    /// </summary>
    private readonly string _content = content ?? string.Empty;

    /// <summary>
    /// The headers of the web request.
    /// </summary>
    private NameValueCollection? _headers;

    /// <summary>
    /// The host name of the web request.
    /// </summary>
    private string? _hostName;

    /// <summary>
    /// The port of the web request.
    /// </summary>
    private int _port;

    /// <summary>
    /// The request path of the web request.
    /// </summary>
    private string? _requestPath = null;

    /// <summary>
    /// Gets the headers of the web request.
    /// </summary>
    public NameValueCollection Headers
    {
        get
        {
            _headers ??= ParseHeaders();
            return _headers;
        }
    }

    /// <summary>
    /// Gets the host name of the web request.
    /// </summary>
    public string HostName
    {
        get
        {
            if (_hostName == null)
            {
                var hostHeader = Headers["Host"];
                if (hostHeader != null)
                {
                    var hostParts = hostHeader.Split(':');
                    _hostName = hostParts[0];
                    if (hostParts.Length > 1)
                    {
                        _port = int.Parse(hostParts[1]);
                    }
                }
                else
                {
                    throw new WebSocketUpgradeException("Host header is missing");
                }
            }
            return _hostName;
        }
        private set
        {
            _hostName = value;
        }
    }

    /// <summary>
    /// Gets the port of the web request.
    /// </summary>
    public int Port
    {
        get
        {
            if (_port == 0)
            {
                var hostHeader = Headers["Host"];
                if (hostHeader != null)
                {
                    var hostParts = hostHeader.Split(':');
                    _hostName = hostParts[0];
                    if (hostParts.Length > 1)
                    {
                        _port = int.Parse(hostParts[1]);
                    }
                }
                if (_port == 0)
                {
                    _port = 80;
                }
            }
            return _port;
        }
        private set
        {
            _port = value;
        }
    }

    /// <summary>
    /// Gets the request path of the web request.
    /// </summary>
    public string RequestPath
    {
        get
        {
            if (_requestPath == null)
            {
                var requestLine = StatusLine;
                var parts = requestLine.Split(' ');
                _requestPath = parts[1];
            }
            return _requestPath;
        }
        private set
        {
            _requestPath = value;
        }
    }

    /// <summary>
    /// Parses the headers of the web request.
    /// </summary>
    /// <returns>The parsed headers.</returns>
    private NameValueCollection ParseHeaders()
    {
        var headers = new NameValueCollection();
        var contentLines = GetContentLines();
        for (int i = 1; i < contentLines.Length; i++)
        {
            var line = contentLines[i];
            int separatorIndex = line.IndexOf(':');
            if (separatorIndex > 0)
            {
                // we simply add the header name and value to the collection
                // if a value is comma-separated, we handle it when it is accessed
                string headerName = line[..separatorIndex].Trim();
                string headerValue = line[(separatorIndex + 1)..].Trim();
                headers.Add(headerName, headerValue);
            }
        }
        return headers;
    }

    /// <summary>
    /// Creates a web request context for the specified host name, port, and request path.
    /// </summary>
    /// <param name="hostName">The host name of the web request.</param>
    /// <param name="port">The port of the web request.</param>
    /// <param name="requestPath">The request path of the web request.</param>
    /// <returns>The created web request context.</returns>
    internal static WebContext CreateRequest(string hostName, int port, string requestPath)
    {
        return new WebContext()
        {
            HostName = hostName,
            Port = port,
            RequestPath = requestPath
        };
    }

    /// <summary>
    /// Checks if the web request contains a specific header with a specific value.
    /// </summary>
    /// <param name="name">The name of the header.</param>
    /// <param name="value">The value of the header.</param>
    /// <returns><c>true</c> if the web request contains the specified header with the specified value; otherwise, <c>false</c>.</returns>
    internal bool ContainsHeader(string name, string value)
    {
        string? headerValue = Headers[name];
        return headerValue != null && headerValue.Contains(value, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets a value indicating whether the web request is a WebSocket request.
    /// </summary>
    public bool IsWebSocketRequest
    {
        get
        {
            bool hasNoConnectionHeader = string.IsNullOrEmpty(Headers["Connection"]);
            bool hasNoUpgradeHeader = string.IsNullOrEmpty(Headers["Upgrade"]);

            if (hasNoConnectionHeader || hasNoUpgradeHeader)
            {
                return false;
            }

            var connectionValues = GetAllHeaderValues("Connection");
            foreach (var connectionValue in connectionValues!)
            {
                if (string.Compare(connectionValue, "Upgrade", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return true;
                }
            }

            var upgradeValues = GetAllHeaderValues("Upgrade");
            foreach (var upgradeValue in upgradeValues)
            {

                if (string.Compare(upgradeValue, "websocket", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Gets all values of the specified header. 
    /// </summary>
    /// <remarks>
    /// If the header value is comma-separated, it is split into separate values.
    /// </remarks>
    /// <param name="headerName">The name of the header.</param>
    /// <returns>All values of the specified header.</returns>
    internal IEnumerable<string> GetAllHeaderValues(string headerName)
    {
        var values = Headers.GetValues(headerName);
        if (values == null)
        {
            yield break;
        }
        foreach (var value in values)
        {
            foreach (var subValue in value.Split(','))
            {
                yield return subValue.Trim();
            }
        }
    }


    /// <summary>
    /// Gets the concatenated headers of the web request.
    /// </summary>
    /// <remarks>
    /// Headers are concatenated with a comma and a space.
    /// </remarks>
    /// <param name="headerName">The name of the header.</param>
    /// <returns>A concatenated string of the header values if header exists; otherwise, <c>null</c>.</returns>
    internal string? GetConcatenatedHeaders(string headerName)
    {
        var values = Headers.GetValues(headerName);
        if (values == null)
        {
            return null;
        }
        return string.Join(", ", values);
    }

    /// <summary>
    /// Gets the status line of the web request.
    /// </summary>
    public string StatusLine
    {
        get
        {
            string[] lines = GetContentLines();
            if (lines.Length == 0) throw new WebSocketUpgradeException("Status line is missing");
            return lines[0];
        }
    }

    /// <summary>
    /// Gets the request line of the web request.
    /// </summary>
    public string RequestLine
    {
        get
        {
            string[] lines = GetContentLines();
            if (lines.Length == 0) throw new WebSocketUpgradeException("Request line is missing");
            return lines[0];
        }
    }

    /// <summary>
    /// Gets the content lines of the web request.
    /// </summary>
    /// <returns>The content lines.</returns>
    private string[] GetContentLines()
    {
        if (_content == null)
        {
            throw new WebSocketUpgradeException("Content is missing");
        }
        var lines = _content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        return lines;
    }
}
