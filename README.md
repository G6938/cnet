<p align="center"><img src="assets/logo.png" width="140" alt="cnet"></p>

# cnet

<p align="center">
  <a href="https://github.com/G6938/cnet/actions/workflows/ci.yml"><img src="https://github.com/G6938/cnet/actions/workflows/ci.yml/badge.svg" alt="CI"></a>
  <a href="https://www.nuget.org/packages/cnet"><img src="https://img.shields.io/nuget/v/cnet.svg" alt="NuGet"></a>
  <a href="https://www.nuget.org/packages/cnet"><img src="https://img.shields.io/nuget/dt/cnet.svg" alt="Downloads"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-blue.svg" alt="License"></a>
  <img src="https://img.shields.io/badge/.NET-9.0-512BD4.svg" alt=".NET 9">
</p>

High-level Telegram bot toolkit for .NET 9. Full Bot API access through `Telegram.Bot`, plus the production plumbing every serious bot needs: automatic 429 retry, fluent keyboards, command and callback routing, middleware pipeline, bounded update queue with parallel background workers, long polling, and a secure ASP.NET Core webhook.

## Packages

| Package | Purpose |
|---|---|
| `cnet` | Core: client, routing, pipeline, queue, polling |
| `cnet.aspnetcore` | Webhook endpoint with secret-token validation and auto registration |

## Quick start (polling)

```csharp
var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddCnet(options => options.BotToken = builder.Configuration["BotToken"]!)
    .UseReplayGuard()
    .OnCommand("start", async ctx =>
    {
        var keyboard = Keyboard.Inline()
            .Row().Callback("Help", "help").Url("Site", "https://example.com")
            .Build();

        await ctx.Client.SendTextAsync(ctx.ChatId, "<b>Hello!</b>", keyboard);
    })
    .OnCallback("help", async ctx =>
    {
        await ctx.AnswerAsync();
        await ctx.Client.EditTextAsync(ctx.ChatId!.Value, ctx.MessageId!.Value, "Help text");
    })
    .OnMessage(async ctx =>
    {
        await ctx.TypingAsync();
        await ctx.ReplyQuotedAsync("Echo: " + ctx.Text);
    })
    .UsePolling();

await builder.Build().RunAsync();
```

## Webhook (ASP.NET Core)

```csharp
builder.Services
    .AddCnet(options => options.BotToken = configuration["BotToken"]!)
    .OnCommand("start", ctx => ctx.ReplyAsync("Hi"));

builder.Services.AddCnetWebhook(options =>
{
    options.PublicUrl = "https://bot.example.com";
    options.Path = "/telegram/webhook";
    options.SecretToken = configuration["WebhookSecret"]!;
});

var app = builder.Build();
app.MapCnetWebhook();
app.Run();
```

## Full Bot API

Every Bot API method is available with automatic retry:

```csharp
await ctx.Client.ExecuteAsync((bot, ct) => bot.SendDice(chatId, cancellationToken: ct));
```

Or use `ctx.Client.Raw` for the untouched `ITelegramBotClient`.

## Highlights

- Automatic retry on 429 (`retry_after` respected) and transient network errors
- Proactive outbound throttle: 30 msg/s global, 1 msg/s per chat (configurable, on by default)
- Sessions and FSM: `ctx.Session()` state + typed data, `OnState("step", ...)` routing, pluggable `ISessionStorage`
- Media group aggregation: `OnAlbum(...)` receives whole albums as one event
- Inbound per-user rate limiting: `UseRateLimit(30)`
- Global error hook: `OnError(ctx => ...)`
- Class-based handlers with constructor DI: `OnCommand<StartHandler>()`
- Localization: `AddTexts(...)` + `ctx.T("key")` keyed by the user's language
- Bounded update queue with backpressure, configurable worker concurrency, graceful shutdown
- Longest-prefix callback routing; unmatched callbacks are auto-answered
- Middleware pipeline (`IUpdateMiddleware`) with built-in replay guard
- Fluent inline and reply keyboard builders, file download helper
- Constant-time webhook secret validation

## Documentation

- [Changelog](CHANGELOG.md)
- [Contributing guide](CONTRIBUTING.md)
- [Security policy](SECURITY.md)
- [Code of conduct](CODE_OF_CONDUCT.md)

## Contributing

Contributions are welcome. Read the [contributing guide](CONTRIBUTING.md)
first — it covers development setup, coding standards, and the pull request
process. For security issues follow the [security policy](SECURITY.md)
instead of opening a public issue.

## License

Copyright (c) 2026 The cnet Authors and contributors. Released under the
[MIT License](LICENSE).

Telegram is a trademark of Telegram FZ-LLC. This project is an independent,
community-maintained library and is not affiliated with, sponsored, or
endorsed by Telegram.
