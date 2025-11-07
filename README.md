# SimpleWebSocket

Jung.SimpleWebSocket is a lightweight and easy-to-use library for working with WebSocket connections in .NET. \
It is built on top of the `System.Net.WebSockets` namespace and provides a simple API for creating WebSocket clients and servers. \
By using a TcpListener and TcpClient, Jung.SimpleWebSocket is able to handle WebSocket connections without the need for a full-fledged HTTP server.
You also don't need admin rights to run the server.

## Status

This project is currently under development and is not yet complete.

## Usage

Using Jung.SimpleWebSocket is straightforward. For ready-to-run server and client examples, see the `examples/` folder in this repository.

The `examples/` directory contains small sample projects demonstrating how to configure and run a server, connect a client, send/receive messages, and configure logging. Copy or open the example projects in your IDE to get started quickly.

### Examples overview

The `examples/` folder contains four small projects demonstrating common usage patterns:

1. `BasicClientExample` — Minimal client demonstrating how to connect to a server and send/receive text messages.
2. `BasicServerExample` — Minimal server demonstrating how to accept WebSocket connections and handle simple messages.
3. `BasicUserHandlingClientExample` — Client example that sends a username on connect and handles acceptance or rejection responses.
4. `BasicUserHandlingServerExample` — Server example that validates incoming usernames, accepts or rejects clients when a username is already in use, and demonstrates basic user handling logic.

### Logging Configuration

This library uses the standard [`ILogger<T>`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.ilogger) interface from `Microsoft.Extensions.Logging`.
Log levels can be configured through the application's existing logging configuration (e.g., `appsettings.json`):

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Jung.SimpleWebSocket": "Debug"
    }
  }
}
```

## License

This project is licensed under the MIT License.  
For more details, please refer to the [LICENSE.txt](./LICENSE.txt) file.
