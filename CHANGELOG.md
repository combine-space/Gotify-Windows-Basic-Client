# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.2.1] - 2026-10-28

### Changed

- Header text corrected from "Notifications Center" to "Notification Center"

### Fixed

- About dialog showed a hardcoded "v1.0"; it now shows the actual app version

### Security

- Bundled .NET runtime updated from 10.0.8 to 10.0.14, including the security fixes of the 10.0.13 and 10.0.14 servicing releases

## [1.2.0] - 2026-09-11

### Added

- Message bodies render markdown (bold, links, inline code, fenced code blocks) instead of showing raw `**syntax**`
- Fenced code blocks get a dark-themed, read-only editor view matching the app
- Notification badge shows the app's own Gotify icon when available

### Changed

- Long messages are no longer truncated, cards grow to fit the full text
- Notification badge falls back to a centered priority glyph when no app icon is available

### Fixed

- Off-center exclamation mark in the notification badge
- Mouse-wheel scrolling was swallowed while hovering over a message body

## [1.1.0] - 2026-08-15

### Added

- Automatic WebSocket reconnect with backoff when the connection to Gotify drops
- App, taskbar and tray icon use the official Gotify logo (CC BY 4.0, unofficial client)

### Changed

- Settings icon redesigned as a vector graphic
- `build.bat` points to the current .NET 10 SDK download instead of a pinned old version

### Fixed

- Notification bell alignment
- Low-contrast options (...) menu icon

## [1.0.1] - 2026-05-08

### Changed

- UI strings, error messages and comments translated from French to English
- Target framework upgraded to .NET 10

### Removed

- Redundant `System.Text.Encodings.Web` dependency

## [1.0.0] - 2026-02-02

### Added

- Initial release of the community fork: WebSocket connection to a Gotify server with real-time notifications
- Native Windows notifications, system tray icon with minimize-to-tray
- Received message list and automatic configuration persistence
- Dark theme, auto-connect on startup

### Fixed

- Message timestamps

[Unreleased]: https://github.com/combine-space/Gotify-Windows-Basic-Client/compare/v1.2.1...HEAD
[1.2.1]: https://github.com/combine-space/Gotify-Windows-Basic-Client/compare/v1.2.0...v1.2.1
[1.2.0]: https://github.com/combine-space/Gotify-Windows-Basic-Client/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/combine-space/Gotify-Windows-Basic-Client/compare/v1.0.1...v1.1.0
[1.0.1]: https://github.com/combine-space/Gotify-Windows-Basic-Client/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/combine-space/Gotify-Windows-Basic-Client/releases/tag/v1.0.0
