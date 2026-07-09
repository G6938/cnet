# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.7.0] - 2026-07-09

### Added

- **Distributed album aggregation:** media-group buffering now runs through an
  `IAlbumStore`. With `UseRedis`, album parts that arrive on different
  instances are buffered in Redis and flushed together, so albums no longer
  require sticky sessions behind a load balancer.
- **cnet.testing** package: `BotTestHarness` drives commands, messages, and
  callbacks through your real handler pipeline with a `FakeBotClient`, and lets
  you assert on outgoing requests — no Telegram connection required.
- `samples/EchoBot`: a complete runnable bot showing commands, a session-based
  form, callbacks, keyboards, and rate limiting.

### Changed

- Album flushing moved to a background service polling the store, replacing the
  per-group timer.

## [1.6.0] - 2026-07-09

### Fixed

- **Durable queue correctness (critical):** the Redis update queue now
  acknowledges an update only after it is fully processed. A worker that
  crashes mid-processing leaves the update pending, and another instance
  reclaims it via `XCLAIM` after the idle window — no more lost updates. The
  in-memory queue uses the same lease model.
- **Webhook enqueue no longer fire-and-forget:** the webhook awaits the write
  and returns 503 if the queue is unavailable, instead of acknowledging an
  update that was never stored.
- Removed a dead semaphore and busy-loop leftovers from the Redis channel.

### Added

- **Distributed outbound throttle** (`UseRedis` replaces the throttle): the
  30 msg/s global and 1 msg/s per-chat limits are now enforced across all
  instances via Redis, so a fleet cannot collectively exceed Telegram's limits.
- **Distributed inbound rate limiting** via `UseRedisRateLimit(n)`.
- `IReplayGuard`, `IOutboundThrottle`, and `IInboundRateLimiter` abstractions
  so every protection can be swapped for a distributed implementation.
- Redis integration test suite covering durable delivery, stale-entry reclaim,
  cross-instance replay protection, and shared sessions.

### Changed

- `IUpdateChannel` now hands out an `UpdateLease` that is completed after
  successful processing, replacing the previous try-dequeue model.

## [1.5.0] - 2026-07-09

### Added

- **cnet.redis** package: durable update queue on Redis Streams (survives
  process crashes, shared across instances), distributed replay protection,
  and Redis-backed session storage. Enable with `builder.UseRedis(...)`.
- **cnet.metrics** package: OpenTelemetry-compatible metrics (updates received,
  processed, failed, and processing latency) via a standard
  `System.Diagnostics.Metrics` meter named `Cnet`. Enable with
  `builder.UseMetrics()`.
- `IReplayGuard` abstraction so replay protection can be swapped for a
  distributed implementation

## [1.4.0] - 2026-07-09

### Added

- `ctx.Bot` on every context: the raw `ITelegramBotClient` with all 180+ Bot
  API methods and full IntelliSense, one property away
- Wrapper methods for every common media and management operation: video,
  audio, voice, animation, video note, location, contact, poll, dice, media
  group, caption/markup edits, pin/unpin, ban/unban, get chat and chat member

## [1.3.0] - 2026-07-09

### Added

- Bot API 10.1 support: upgraded to Telegram.Bot 22.10.1.1, with convenience
  helpers `SendRichMessageAsync` and `AnswerJoinRequestQueryAsync`; all other
  10.1 methods are reachable through the raw client
- Comprehensive README: step-by-step installation, sessions/FSM, albums,
  middleware, rate limiting, webhooks, and a full configuration reference

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

[1.7.0]: https://github.com/G6938/cnet/releases/tag/v1.7.0
[1.6.0]: https://github.com/G6938/cnet/releases/tag/v1.6.0
[1.5.0]: https://github.com/G6938/cnet/releases/tag/v1.5.0
[1.4.0]: https://github.com/G6938/cnet/releases/tag/v1.4.0
[1.3.0]: https://github.com/G6938/cnet/releases/tag/v1.3.0
[1.2.2]: https://github.com/G6938/cnet/releases/tag/v1.2.2
[1.2.1]: https://github.com/G6938/cnet/releases/tag/v1.2.1
[1.2.0]: https://github.com/G6938/cnet/releases/tag/v1.2.0
[1.1.0]: https://github.com/G6938/cnet/releases/tag/v1.1.0
[1.0.0]: https://github.com/G6938/cnet/releases/tag/v1.0.0
