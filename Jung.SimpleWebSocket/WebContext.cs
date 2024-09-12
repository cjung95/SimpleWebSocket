using System.Collections.Specialized;

namespace Jung.SimpleWebSocket;

internal class WebContext
{
    private readonly string _content;
    private NameValueCollection? _headers;

    public WebContext(string? content = null)
    {
        _content = content ?? string.Empty;
    }

    public NameValueCollection Headers
    {
        get
        {
            _headers ??= ParseHeaders();
            return _headers;
        }
    }

    private NameValueCollection ParseHeaders()
    {
        var headers = new NameValueCollection();
        var requestLines = _content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        for (int i = 1; i < requestLines.Length; i++)
        {
            var line = requestLines[i];
            int separatorIndex = line.IndexOf(':');
            if (separatorIndex > 0)
            {
                string headerName = line[..separatorIndex].Trim();
                line[(separatorIndex + 1)..].Split(',').ToList().ForEach(x => headers.Add(headerName, x.Trim()));
            }
        }
        return headers;
    }

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

            var connectionValues = Headers.GetValues("Connection");
            foreach (var connectionValue in connectionValues!)
            {
                if (string.Compare(connectionValue, "Upgrade", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return true;
                }
            }

            var upgradeValues = Headers.GetValues("Upgrade");
            foreach (var upgradeValue in upgradeValues!)
            {
                if (string.Compare(upgradeValue, "websocket", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
