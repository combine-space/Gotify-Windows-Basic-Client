# Gotify Windows Client

Native Windows client in C# WPF to receive notifications from a Gotify server.

## Features

- ✅ WebSocket connection to Gotify with real-time notification reception
- ✅ Automatic reconnect with backoff if the connection drops
- ✅ Markdown rendering of message bodies (bold, links, inline code, fenced code blocks with a dark-themed view)
- ✅ Native Windows notifications (balloon tips) with optional sound
- ✅ Gotify app icons on messages, with a priority indicator as fallback
- ✅ System tray icon with automatic minimize-to-tray
- ✅ Received message list, long messages are shown in full
- ✅ Automatic configuration persistence

## Requirements

- Windows 10/11
- A Gotify server with a client token
- .NET 10.0 SDK, only if you build from source (the release download needs no .NET installation)

## Installation

### Option 1: Download a release

1. Download the latest `GotifyClient-vX.Y.Z-win-x64.zip` from the [Releases page](https://github.com/combine-space/Gotify-Windows-Basic-Client/releases)
2. Extract the whole zip into a folder
3. Run `GotifyClient.exe`

Keep the extracted DLLs next to the exe, the app needs them to start.

### Option 2: Build from source

1. Install the [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
2. Open a terminal in the project directory
3. Build the project:

```bash
dotnet build -c Release
```

The executable will be in: `bin/Release/net10.0-windows/GotifyClient.exe`

### Option 3: Publish a self-contained build

To create a standalone executable that does not require .NET to be installed:

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The executable will be in: `bin/Release/net10.0-windows/win-x64/publish/GotifyClient.exe`

## Usage

1. Launch `GotifyClient.exe`
2. Configure the connection:
   - **Server URL**: The URL of your Gotify server (e.g. `https://gotify.example.com`)
   - **Client Token**: Your client application token (create it in the Gotify UI)
3. Click "Connect"
4. The app connects and starts receiving notifications

### Get a client token

1. Sign in to your Gotify web UI
2. Go to **Applications**
3. Create a new application
4. Copy the generated **token**
5. Paste it into the "Client Token" field in the app

## Configuration

The configuration (server URL and token) is saved automatically to `gotify_config.json` in the same folder as the executable.

## Behavior

- **Options menu (...)**: Toggles for sound notifications, Windows notifications and minimize to tray, plus About and Quit
- **Minimize to tray**: Enable this option to minimize the app to the system tray instead of the taskbar
- **Double-click the tray icon**: Restores the window
- **Right-click the tray icon**: Shows the context menu (Open/Quit)
- **Clear all**: Removes the displayed message history (does not delete messages on the server)
- **Dropped connection**: If the WebSocket connection is lost, the app automatically retries in the background with increasing delays (3s, 6s, 12s, 30s, then every 60s) until it reconnects. No manual action needed.

## Troubleshooting

### The app cannot connect

- Ensure the server URL is correct (including `https://` or `http://`)
- Verify the token is valid
- Confirm the Gotify server is reachable from your network
- Ensure the WebSocket port is open (usually the same as HTTP/HTTPS)
- If the connection was working before and just dropped, give it a moment, the app retries automatically. Use the settings dialog only if you need to change the server URL or token.

### Notifications do not appear

- Confirm Windows notifications are enabled in system settings
- Test by sending a message from the Gotify web UI

### SSL certificate error

If you use a self-signed certificate, you may need to update the code to accept invalid certificates (not recommended for production).

## Technologies used

- C# / .NET 10
- WPF (Windows Presentation Foundation)
- WebSocket for real-time connectivity
- System.Windows.Forms for the tray icon
- [MdXaml](https://github.com/whistyun/MdXaml) for markdown rendering

## Security

The release zips are self-contained and bundle the .NET runtime, so the app is only as current as the runtime it was built with. Each release is built with the latest .NET servicing SDK. Runtime updates are noted in the [changelog](CHANGELOG.md), and the fixes of every servicing release are documented in the [.NET 10 release notes](https://github.com/dotnet/core/tree/main/release-notes/10.0).

If you build from source or use a framework-dependent build, keep your installed .NET runtime up to date.

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for the release history. It follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## Credits

This is an unofficial, community-built client and is not affiliated with the Gotify project.

The Gotify logo is used under the [Creative Commons Attribution 4.0 International License](https://github.com/gotify/logo) (c) gotify/logo contributors.

## License

MIT License - Free to use and modify

## Support

To report a bug or request a feature, open an issue on the GitHub repository.
