# SimpleWebSocket

Jung.SimpleWebSocket is a lightweight and easy-to-use library for working with WebSocket connections in .NET. \
It is built on top of the `System.Net.WebSockets` namespace and provides a simple API for creating WebSocket clients and servers. \
By using a TcpListener and TcpClient, Jung.SimpleWebSocket is able to handle WebSocket connections without the need for a full-fledged HTTP server.
You also don't need admin rights to run the server.

## Status

This project is currently under development and is not yet complete.  

## Usage

Using Jung.SimpleWebSocket is straightforward. Here's a simple example for the server:

```csharp
// Import the Jung.SimpleWebSocket namespace
using Jung.SimpleWebSocket;

// Create a WebSocket server
var server = new SimpleWebSocketServer(System.Net.IPAddress.Any, 8010);
server.ClientConnected += (sender, e) => System.Console.WriteLine($"Client connected");
server.MessageReceived += (sender, e) => System.Console.WriteLine($"Message received: {e.Message}");
server.ClientDisconnected += (sender, e) => System.Console.WriteLine($"Client disconnected");
server.Start(CancellationToken.None);
```

And here's a simple example for the client:

```csharp
// Import the Jung.SimpleWebSocket namespace
using Jung.SimpleWebSocket;

// Create a WebSocket client and send "Hello World!"
var client = new SimpleWebSocketClient(System.Net.IPAddress.Loopback.ToString(), 8010, "/");
client.MessageReceived += (sender, e) => Console.WriteLine(e.Message);
client.BinaryMessageReceived += (sender, e) => Console.WriteLine(e.Message);
client.Disconnected += (sender, e) => Console.WriteLine("Disconnected");
await client.ConnectAsync(CancellationToken.None);
await client.SendMessageAsync("Hello World!", CancellationToken.None);
await client.DisconnectAsync();
```

## License

This project is licensed under the MIT License.  
For more details, please refer to the [LICENSE.txt](./LICENSE.txt) file.
