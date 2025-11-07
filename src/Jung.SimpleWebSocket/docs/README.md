# Jung.SimpleWebSocket

Jung.SimpleWebSocket is library for working with WebSocket connections in .NET. 
It is built on top of the `System.Net.WebSockets` namespace and provides a simple API for creating WebSocket clients and servers.
The library is designed to give you full access to the WebSocket connection process. 
You also don't need admin rights to bind the server to a port, as the library uses the `HttpListener` class to listen for incoming WebSocket connections.

## Installation

You can install Jung.SimpleWebSocket via NuGet package manager or by manually downloading and building the library.

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

Using Jung.SimpleWebSocket is straightforward. For ready-to-run server and client examples, see the `examples/` folder on the [Jung.SimpleWebSocket GitHub repository](https://github.com/cjung95/SimpleWebSocket).

## Contributing

Contributions are welcome! If you find a bug or have a feature request, please open an issue on the [Jung.SimpleWebSocket GitHub repository](https://github.com/cjung95/SimpleWebSocket/issues).

## License

Jung.SimpleWebSocket is licensed under the [MIT License](https://github.com/cjung95/SimpleWebSocket/blob/main/LICENSE.txt).
