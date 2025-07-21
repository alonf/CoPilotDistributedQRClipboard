# Distributed QR Clipboard

A real-time distributed clipboard sharing system using QR codes and SignalR for seamless content synchronization across devices.

## Features

- 🔄 **Real-time Clipboard Sharing**: Instantly sync clipboard content across multiple devices
- 📱 **QR Code Generation**: Server-side QR code generation for easy session joining
- 🌐 **Cross-Device Support**: Works across different browsers and devices
- 🎨 **Modern UI**: Professional, responsive web interface with gradient design
- ⚡ **SignalR Integration**: Real-time bidirectional communication
- 🔒 **Session Management**: Secure session-based device authorization
- 🧪 **Comprehensive Testing**: Unit tests and Selenium integration tests

## Architecture

The system is built with a clean, layered architecture:

- **DistributedQRClipboard.Api**: ASP.NET Core web API with SignalR hubs
- **DistributedQRClipboard.Core**: Core business logic and models
- **DistributedQRClipboard.Infrastructure**: Data access and external services
- **DistributedQRClipboard.Tests**: Unit and integration tests

## Technologies Used

- **.NET 9**: Latest .NET framework
- **ASP.NET Core**: Web API and hosting
- **SignalR**: Real-time communication
- **QRCoder**: Server-side QR code generation
- **Selenium WebDriver**: End-to-end testing
- **xUnit**: Unit testing framework
- **FluentAssertions**: Better test assertions

## Getting Started

### Prerequisites

- .NET 9 SDK
- Visual Studio 2022 or VS Code
- Chrome/Chromium browser (for Selenium tests)

### Running the Application

1. Clone the repository:
   ```bash
   git clone https://github.com/alonf/CoPilotDistributedQRClipboard.git
   cd CoPilotDistributedQRClipboard
   ```

2. Build the solution:
   ```bash
   dotnet build
   ```

3. Run the application:
   ```bash
   cd DistributedQRClipboard.Api
   dotnet run
   ```

4. Open your browser and navigate to `http://localhost:5000`

### Running Tests

Run all tests:
```bash
dotnet test
```

Run specific test categories:
```bash
# Unit tests only
dotnet test --filter "ClipboardManagerTests"

# Selenium tests only
dotnet test --filter "*Selenium*"
```

## How It Works

1. **Session Creation**: When you open the app, it automatically creates a new clipboard session
2. **QR Code Generation**: A QR code is generated server-side containing the session URL
3. **Device Joining**: Other devices can scan the QR code or visit the session URL to join
4. **Real-time Sync**: Any text typed in one device instantly appears on all connected devices
5. **Device Management**: The system tracks connected devices and manages session state

## Key Features Implemented

### Session Management
- Automatic session creation and cleanup
- Device authorization and tracking
- Session expiration handling
- Maximum device limits per session

### Real-time Communication
- SignalR hubs for bidirectional communication
- Automatic reconnection handling
- Event-based clipboard updates
- Device join/leave notifications

### QR Code Generation
- Server-side QR code generation using QRCoder
- No client-side JavaScript dependencies
- Optimized image generation API
- Responsive QR code display

### Testing Strategy
- **Unit Tests**: Core business logic validation
- **Integration Tests**: SignalR hub functionality
- **Selenium Tests**: End-to-end browser automation
- **Error Detection**: JavaScript error monitoring

## Development Journey

This project was developed iteratively with a focus on:

1. **Clean Architecture**: Separation of concerns across multiple projects
2. **Real-time Features**: SignalR implementation for instant synchronization
3. **Professional UI**: Modern, responsive design with CSS gradients
4. **Robust Testing**: Comprehensive test coverage with multiple test types
5. **Error Handling**: Proper error detection and user feedback
6. **Device Authorization**: Secure session-based device management

## API Endpoints

- `POST /api/sessions` - Create a new clipboard session
- `GET /api/qrcode` - Generate QR code images
- `/clipboardHub` - SignalR hub for real-time communication

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests for new functionality
5. Ensure all tests pass
6. Submit a pull request

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Acknowledgments

- Built with GitHub Copilot assistance
- Uses QRCoder library for QR code generation
- Powered by SignalR for real-time communication
