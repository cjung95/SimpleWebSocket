# SimpleWebSocket

A simple TcpListener-based WebSocket.

## Status

This project is currently under development and is not yet complete.  
At this stage, **the server can handle only one client at a time**.  
Support for multiple clients will be added in the near future.

## Usage

Using Jung.SimpleWebSocket is straightforward. Here's a simple example for the server:

```csharp
// Import the Jung.SimpleWebSocket namespace
using Jung.SimpleWebSocket;

// Create a WebSocket server
var server = new SimpleWebSocketServer(System.Net.IPAddress.Any, 8010);
server.ClientConnected += (o) => System.Console.WriteLine($"Client connected");
server.MessageReceived += (m) => System.Console.WriteLine($"Message received: {m}");
server.ClientDisconnected += (o) => System.Console.WriteLine($"Client disconnected");
server.Start(CancellationToken.None);
```

And here's a simple example for the client:

```csharp
// Import the Jung.SimpleWebSocket namespace
using Jung.SimpleWebSocket;

// Create a WebSocket client and send "Hello World!"
var client = new SimpleWebSocketClient(IPAddress.Loopback.ToString(), 8010, "/");
client.MessageReceived += (m) => Console.WriteLine(m);
await client.ConnectAsync(CancellationToken.None);
await client.SendMessageAsync("Hello World!", CancellationToken.None);
await client.DisconnectAsync();
```

## License

This project is licensed under the MIT License.  
For more details, please refer to the [LICENSE.txt](./LICENSE.txt) file.
