# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.2.2] - 2026-07-09

### Fixed

- Dedicated plain-Markdown package readme: NuGet.org strips HTML, so the
  package now ships a NuGet-compatible readme while the repository keeps the
  rich one

## [1.2.1] - 2026-07-09

### Fixed

- Package readme now renders correctly on NuGet.org: absolute logo and
  documentation links
- Added copyright metadata to package manifests

## [1.2.0] - 2026-07-09

### Added

- Rich context API on message and command contexts: `ReplyQuotedAsync`,
  `ReplyWithPhotoAsync`, `ReplyWithDocumentAsync`, `ReplyWithStickerAsync`,
  `ReactAsync`, `DeleteAsync`, `CopyToAsync`, `ForwardToAsync`, `TypingAsync`,
  and the `Text` shortcut that falls back to captions
- Rich callback context API: `EditTextAsync`, `DeleteMessageAsync`,
  `ReplyAsync`, `AlertAsync`
- `CnetClient.ForwardAsync` and `CnetClient.SendChatActionAsync`
- Repository metadata in package manifests and GitHub Packages publishing on
  release

## [1.1.0] - 2026-07-09

### Added

- Sessions and FSM: `Session()` with typed data and state, `OnState(...)`
  routing, pluggable `ISessionStorage` with in-memory default
- Proactive outbound throttle (30 msg/s global, 1 msg/s per chat), on by default
- Inbound per-user rate limiting via `UseRateLimit(...)`
- Media group aggregation via `OnAlbum(...)`
- Global error hook via `OnError(...)`
- Class-based handlers with constructor injection:
  `OnCommand<T>()`, `OnCallback<T>()`, `OnMessage<T>()`
- Filtered message handlers: `OnMessage(filter, handler)`
- Localization: `AddTexts(...)` catalog and `T(...)` keyed by user language
- File download helper `CnetClient.DownloadFileAsync`
- Package icon

### Fixed

- Transient network errors wrapped in `RequestException` are now retried
- Long polling applies backpressure instead of silently dropping updates when
  the queue is full
- HTTP connections are recycled (`PooledConnectionLifetime`), fixing stale DNS
  in long-running bots
- Callback queries that match no handler are automatically answered, so
  clients no longer show an endless loading spinner
- `PollingService` yields at startup so synchronous fast paths cannot block
  host startup
- Integer overflow in the outbound throttle on the first send to a chat
- Duplicate hosted services when `AddCnet`/`UsePolling` is called twice

### Changed

- `UpdatePipeline` builds its middleware chain once per scope instead of per
  update

## [1.0.0] - 2026-07-09

### Added

- Initial release: `CnetClient` with full Bot API access and automatic 429
  retry, command/callback/message routing, middleware pipeline with replay
  guard, bounded update queue with parallel workers, long polling, fluent
  keyboards, and ASP.NET Core webhook integration with secret-token validation
  and automatic registration

[1.2.2]: https://github.com/G6938/cnet/releases/tag/v1.2.2
[1.2.1]: https://github.com/G6938/cnet/releases/tag/v1.2.1
[1.2.0]: https://github.com/G6938/cnet/releases/tag/v1.2.0
[1.1.0]: https://github.com/G6938/cnet/releases/tag/v1.1.0
[1.0.0]: https://github.com/G6938/cnet/releases/tag/v1.0.0
