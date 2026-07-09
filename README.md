<p align="center"><img src="https://raw.githubusercontent.com/G6938/cnet/main/assets/logo.png" width="140" alt="cnet"></p>

<h1 align="center">cnet</h1>

<p align="center">
  <a href="https://github.com/G6938/cnet/actions/workflows/ci.yml"><img src="https://img.shields.io/github/actions/workflow/status/G6938/cnet/ci.yml?branch=main&label=CI" alt="CI"></a>
  <a href="https://www.nuget.org/packages/cnet"><img src="https://img.shields.io/nuget/v/cnet.svg" alt="NuGet"></a>
  <a href="https://www.nuget.org/packages/cnet"><img src="https://img.shields.io/nuget/dt/cnet.svg" alt="Downloads"></a>
  <a href="https://github.com/G6938/cnet/blob/main/LICENSE"><img src="https://img.shields.io/badge/license-MIT-blue.svg" alt="License"></a>
  <img src="https://img.shields.io/badge/.NET-9.0-512BD4.svg" alt=".NET 9">
  <img src="https://img.shields.io/badge/Bot%20API-10.1-2CA5E0.svg" alt="Bot API 10.1">
</p>

<p align="center">
  A Telegram bot toolkit for .NET 9. Strongly typed handlers, built-in flood
  control and automatic retry, sessions and FSM, media-group aggregation,
  long polling, and hardened webhooks — with full access to the latest
  Telegram Bot API.
</p>

---

## Table of contents

- [Why cnet](#why-cnet)
- [Installation](#installation)
- [Your first bot in 5 minutes](#your-first-bot-in-5-minutes)
- [Handlers](#handlers)
- [Sending messages](#sending-messages)
- [Keyboards](#keyboards)
- [Sessions and multi-step forms (FSM)](#sessions-and-multi-step-forms-fsm)
- [Albums (media groups)](#albums-media-groups)
- [Middleware](#middleware)
- [Rate limiting and flood control](#rate-limiting-and-flood-control)
- [Error handling](#error-handling)
- [Localization](#localization)
- [Webhooks (production)](#webhooks-production)
- [Full Bot API access](#full-bot-api-access)
- [Bot API 10.1 support](#bot-api-101-support)
- [Configuration reference](#configuration-reference)
- [Contributing](#contributing)
- [License](#license)

## Why cnet

Most bot frameworks hand every handler the same mutable `ctx` object and leave
production concerns — flood limits, retries, back-pressure — to plugins or to
you. cnet takes a different position:

- **Typed turns.** Each interaction has its own handler and its own context
  with exactly the operations it supports. The compiler rules out mistakes
  before the bot runs.
- **Production plumbing in the core.** Outbound throttling, 429 retry, a
  bounded queue with back-pressure, replay protection, and graceful shutdown
  are built in and on by default.
- **No ceiling.** Anything the toolkit does not wrap is one call away with the
  same retry policy through `Client.ExecuteAsync` or the raw
  `ITelegramBotClient`.

## Packages

| Package | Purpose |
|---|---|
| `cnet` | Core toolkit: client, routing, pipeline, sessions, polling |
| `cnet.aspnetcore` | ASP.NET Core webhook endpoint |
| `cnet.redis` | Durable queue, distributed replay guard, shared sessions |
| `cnet.metrics` | OpenTelemetry-compatible metrics |

## Installation

### 1. Create a project

```bash
dotnet new worker -n MyBot
cd MyBot
```

### 2. Add the package

```bash
dotnet add package cnet
```

For webhooks (optional), also add:

```bash
dotnet add package cnet.aspnetcore
```

### 3. Get a bot token

Open [@BotFather](https://t.me/BotFather) in Telegram, send `/newbot`, and
copy the token it gives you (looks like `123456:ABC-DEF...`).

### 4. Store the token safely

Never hardcode the token. Use user secrets in development:

```bash
dotnet user-secrets init
dotnet user-secrets set "BotToken" "123456:ABC-DEF..."
```

In production, use an environment variable named `BotToken`.

## Your first bot in 5 minutes

`Program.cs`:

```csharp
using Cnet.DependencyInjection;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddCnet(options => options.BotToken = builder.Configuration["BotToken"]!)
    .UseReplayGuard()
    .OnCommand("start", async cmd =>
    {
        await cmd.ReplyAsync("<b>Welcome!</b> Send me anything.");
    })
    .OnMessage(async msg =>
    {
        await msg.ReplyQuotedAsync("You said: " + msg.Text);
    })
    .UsePolling();

await builder.Build().RunAsync();
```

Run it:

```bash
dotnet run
```

Message your bot on Telegram — it replies. That is a complete, production-grade
bot: throttled, retried, and crash-resistant out of the box.

## Handlers

Register handlers on the builder. Each receives a typed context.

```csharp
services.AddCnet(o => o.BotToken = token)
    .OnCommand("help", cmd => cmd.ReplyAsync("Help text"))            // /help
    .OnCommand("echo", cmd => cmd.ReplyAsync(cmd.Arguments))          // /echo hi -> "hi"
    .OnCallback("buy:", cb => cb.AnswerAsync("Bought " + cb.Payload)) // callback_data "buy:42"
    .OnMessage(msg => msg.ReplyAsync("Got it"))                       // any non-command message
    .OnUpdate(UpdateType.MyChatMember, u => Task.CompletedTask);      // raw update by type
```

### Class-based handlers with dependency injection

For larger bots, put each handler in its own class and inject services:

```csharp
public sealed class StartHandler(IUserRepository users) : ICommandHandler
{
    public static string Command => "start";

    public async Task HandleAsync(CommandContext cmd)
    {
        await users.EnsureAsync(cmd.UserId);
        await cmd.ReplyAsync("Welcome back!");
    }
}

services.AddCnet(o => o.BotToken = token).OnCommand<StartHandler>();
```

The handler is resolved from a fresh DI scope per update, so scoped services
such as a `DbContext` work as expected.

## Sending messages

Every message and command context exposes shortcuts (all throttled and
retried):

```csharp
await msg.ReplyAsync("plain reply");
await msg.ReplyQuotedAsync("reply that quotes the user's message");
await msg.ReplyWithPhotoAsync("https://example.com/cat.jpg", caption: "A cat");
await msg.ReplyWithDocumentAsync(fileId);
await msg.ReplyWithStickerAsync(stickerFileId);
await msg.ReactAsync("🔥");
await msg.TypingAsync();
await msg.DeleteAsync();
await msg.CopyToAsync(otherChatId);
await msg.ForwardToAsync(otherChatId);
```

The `Client` on any context is the full-featured sender, with wrappers for
every common media type — all throttled and retried:

```csharp
await ctx.Client.SendTextAsync(chatId, "<b>bold</b>");
await ctx.Client.SendPhotoAsync(chatId, url, caption: "hi");
await ctx.Client.SendVideoAsync(chatId, url);
await ctx.Client.SendAudioAsync(chatId, url);
await ctx.Client.SendVoiceAsync(chatId, url);
await ctx.Client.SendAnimationAsync(chatId, url);
await ctx.Client.SendDocumentAsync(chatId, url);
await ctx.Client.SendLocationAsync(chatId, 35.7, 51.4);
await ctx.Client.SendContactAsync(chatId, "+1555", "Sam");
await ctx.Client.SendPollAsync(chatId, "Best?", ["A", "B", "C"]);
await ctx.Client.SendDiceAsync(chatId, "🎲");
await ctx.Client.EditTextAsync(chatId, messageId, "edited");
await ctx.Client.PinAsync(chatId, messageId);
await ctx.Client.BanAsync(chatId, userId);
```

## Keyboards

```csharp
var inline = Keyboard.Inline()
    .Row().Callback("👍", "vote:up").Callback("👎", "vote:down")
    .Row().Url("Website", "https://example.com")
    .Build();

await msg.ReplyAsync("Vote:", inline);

var reply = Keyboard.Reply()
    .Row().Button("Contact").Button("Location")
    .OneTime()
    .Build();
```

## Sessions and multi-step forms (FSM)

A session stores per-user state and typed data. Combine it with `OnState` to
build forms:

```csharp
services.AddCnet(o => o.BotToken = token)
    .OnCommand("register", async cmd =>
    {
        await cmd.Session().SetStateAsync("awaiting_name");
        await cmd.ReplyAsync("What is your name?");
    })
    .OnState("awaiting_name", async msg =>
    {
        await msg.Session().SetAsync("name", msg.Text);
        await msg.Session().SetStateAsync("awaiting_age");
        await msg.ReplyAsync("How old are you?");
    })
    .OnState("awaiting_age", async msg =>
    {
        var name = await msg.Session().GetAsync<string>("name");
        await msg.Session().ClearAsync();
        await msg.ReplyAsync($"Thanks {name}, you are {msg.Text}.");
    })
    .UsePolling();
```

The default storage is in-memory. Implement `ISessionStorage` (for example on
Redis) and register it to share sessions across instances.

## Albums (media groups)

Telegram delivers each photo of an album as a separate update. cnet buffers
them and hands you the whole group at once:

```csharp
services.AddCnet(o => o.BotToken = token)
    .OnAlbum(async album =>
    {
        await album.Client.CopyManyAsync(archiveChatId, album.ChatId, album.MessageIds);
    })
    .UsePolling();
```

## Middleware

Middleware runs for every update before routing:

```csharp
public sealed class LoggingMiddleware(ILogger<LoggingMiddleware> logger) : IUpdateMiddleware
{
    public async Task InvokeAsync(UpdateContext context, UpdateStep next)
    {
        logger.LogInformation("Update {Id}", context.Update.Id);
        await next(context);
    }
}

services.AddCnet(o => o.BotToken = token).Use<LoggingMiddleware>();
```

`UseReplayGuard()` adds built-in protection against duplicate updates.

## Rate limiting and flood control

Two independent protections:

```csharp
services.AddCnet(o =>
    {
        o.BotToken = token;
        // Outbound throttle (on by default): never exceed Telegram's send limits.
        o.EnableOutboundThrottle = true;   // 30 msg/s global, 1 msg/s per chat
    })
    // Inbound: drop updates from a user who exceeds 20 per minute.
    .UseRateLimit(20);
```

Outbound throttling paces your sends *before* they hit Telegram, so you avoid
429 responses instead of merely retrying them.

## Error handling

```csharp
services.AddCnet(o => o.BotToken = token)
    .OnError(async error =>
    {
        logger.LogError(error.Exception, "Update {Id} failed", error.Update.Update.Id);
        // Optionally notify the user or an admin channel.
    });
```

Unhandled exceptions in a handler are logged and passed to every registered
error handler; they never crash the worker.

## Localization

```csharp
services.AddCnet(o => o.BotToken = token)
    .AddTexts(texts =>
    {
        texts.FallbackLocale = "en";
        texts.Add("en", "welcome", "Welcome, {0}!");
        texts.Add("fa", "welcome", "خوش آمدی، {0}!");
    })
    .OnCommand("start", cmd => cmd.ReplyAsync(cmd.T("welcome", cmd.From.FirstName)));
```

`T(...)` picks the text for the user's Telegram language, falling back to the
default locale.

## Webhooks (production)

Polling is fine for development. For production, use webhooks with the
`cnet.aspnetcore` package:

```csharp
using Cnet.AspNetCore;
using Cnet.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddCnet(o => o.BotToken = builder.Configuration["BotToken"]!)
    .OnCommand("start", cmd => cmd.ReplyAsync("Hi"));

builder.Services.AddCnetWebhook(o =>
{
    o.PublicUrl = "https://bot.example.com";              // your public HTTPS URL
    o.Path = "/telegram/webhook";
    o.SecretToken = builder.Configuration["WebhookSecret"]!; // random 32+ char string
});

var app = builder.Build();
app.MapCnetWebhook();
app.Run();
```

The webhook is registered with Telegram automatically at startup, validates
the secret token in constant time, and rejects forged requests. For local
testing, expose your machine with a tunnel such as
[cloudflared](https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/)
and use that URL as `PublicUrl`.

## Full Bot API access

cnet wraps the common operations; everything else is one call away with the
same automatic retry:

Every context exposes `Bot` — the raw `ITelegramBotClient` with all 180+ Bot
API methods and full IntelliSense:

```csharp
// Direct access to any method, with IntelliSense on ctx.Bot:
await ctx.Bot.SendGame(chatId, "my_game");
await ctx.Bot.CreateForumTopic(chatId, "General");
await ctx.Bot.SendGift(userId, giftId);

// Or wrap any call in the automatic retry policy:
await ctx.Client.ExecuteAsync((bot, ct) => bot.SendDice(chatId, cancellationToken: ct));
```

## Bot API 10.1 support

cnet is built on the latest `Telegram.Bot`, so **every Bot API 10.1 feature is
available**, including the June 2026 "Markup Revolution" and Guardian Bots.
Convenience helpers are provided for the headline additions:

```csharp
// Rich messages: headings, lists, tables, LaTeX, media blocks, quotes, and more.
await ctx.Client.SendRichMessageAsync(chatId, richMessage);

// Guardian bots: answer a join-request query (approve / decline / custom result).
await ctx.Client.AnswerJoinRequestQueryAsync(queryId, "approved");
```

Anything not yet wrapped — rich message drafts for streaming, poll option
links, chat-join Mini Apps — is reachable through `Client.Raw`:

```csharp
await ctx.Client.ExecuteAsync((bot, ct) =>
    bot.SendChatJoinRequestWebApp(chatId, userId, webAppQueryId, cancellationToken: ct));
```

Supported 10.1 capabilities include multi-level headings, lists and task
lists, deeply nested inline formatting (spoiler, code, subscript,
superscript), tables, media blocks, block and pull quotes, collapsible
details, anchors, full LaTeX, maps, collages, slideshows, guardian-bot join
queries, and poll option media links.

## Scaling to multiple instances

For a bot that runs behind a load balancer or survives restarts without losing
updates, add `cnet.redis`. It replaces the in-memory queue with a durable Redis
Stream, shares replay protection and sessions across every instance, and keeps
unacknowledged updates until they are processed:

```csharp
using Cnet.Redis;

builder.Services
    .AddCnet(o => o.BotToken = token)
    .UseRedis(r => r.ConnectionString = "localhost:6379")
    .UseReplayGuard()
    .OnCommand("start", cmd => cmd.ReplyAsync("Hi"));
```

## Metrics

Add `cnet.metrics` to expose standard counters and a latency histogram under
the `Cnet` meter, ready for OpenTelemetry or `dotnet-counters`:

```csharp
using Cnet.Metrics;

builder.Services
    .AddCnet(o => o.BotToken = token)
    .UseMetrics();
```

Metrics: `cnet.updates.received`, `cnet.updates.processed`,
`cnet.updates.failed`, and `cnet.update.duration` (ms).

## Configuration reference

`CnetOptions`:

| Option | Default | Description |
|---|---|---|
| `BotToken` | — | Required. Token from @BotFather. |
| `ApiBaseUrl` | Telegram | Override for a local Bot API server. |
| `UpdateQueueCapacity` | 10000 | Bounded queue size before back-pressure. |
| `WorkerConcurrency` | 8 | Parallel update-processing workers. |
| `MaxSendAttempts` | 3 | Retry attempts per API call. |
| `PollingTimeoutSeconds` | 50 | Long-poll timeout. |
| `EnableOutboundThrottle` | true | Pace outgoing messages within Telegram limits. |
| `OutboundGlobalPerSecond` | 30 | Global send rate cap. |
| `OutboundPerChatIntervalMilliseconds` | 1000 | Minimum gap between sends to one chat. |
| `AlbumFlushDelayMilliseconds` | 1500 | How long to buffer a media group. |
| `AllowedUpdates` | all | Update types to receive. |
| `DropPendingUpdates` | false | Discard backlog on startup. |

## Contributing

Contributions are welcome. See the
[contributing guide](https://github.com/G6938/cnet/blob/main/CONTRIBUTING.md)
for development setup and standards, and the
[security policy](https://github.com/G6938/cnet/blob/main/SECURITY.md) for
reporting vulnerabilities privately.

## License

Copyright (c) 2026 The cnet Authors and contributors. Released under the
[MIT License](https://github.com/G6938/cnet/blob/main/LICENSE).

Telegram is a trademark of Telegram FZ-LLC. This project is an independent,
community-maintained library and is not affiliated with, sponsored, or
endorsed by Telegram.
