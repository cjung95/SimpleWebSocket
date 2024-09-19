# Jung.SimpleWebSocket

Jung.SimpleWebSocket is a lightweight and easy-to-use library for working with WebSocket connections in .NET.
It is built on top of the `System.Net.WebSockets` namespace and provides a simple API for creating WebSocket clients and servers.
By using a TcpListener and TcpClient, Jung.SimpleWebSocket is able to handle WebSocket connections without the need for a full-fledged HTTP server.
You also don't need admin rights to run the server.

## Installation

You can install Jung.SimpleWebSocket via NuGet package manager or by manually downloading the library.

### NuGet Package Manager

1. Open the NuGet Package Manager Console in Visual Studio.
2. Run the following command to install the package: `Install-Package Jung.SimpleWebSocket`.
### Manual Download

1. Go to the [Jung.SimpleWebSocket GitHub repository](https://github.com/cjung95/SimpleWebSocket).
2. Click on the "Code" button and select "Download ZIP" to download the library.
3. Extract the ZIP file to a location of your choice.
4. Build the solution in Visual Studio.
5. Add a reference to the `Jung.SimpleWebSocket.dll` file in your project.

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

For more advanced usage and configuration options, please refer to the [documentation](https://github.com/cjung95/SimpleWebSocket/wiki).

## Contributing

Contributions are welcome! If you find a bug or have a feature request, please open an issue on the [Jung.SimpleWebSocket GitHub repository](https://github.com/cjung95/SimpleWebSocket/issues).

## License

Jung.SimpleWebSocket is licensed under the [MIT License](https://github.com/cjung95/SimpleWebSocket/blob/main/LICENSE.txt).
