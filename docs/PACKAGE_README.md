![cnet](https://raw.githubusercontent.com/G6938/cnet/main/assets/logo-128.png)

# cnet

[![CI](https://img.shields.io/github/actions/workflow/status/G6938/cnet/ci.yml?branch=main&label=CI)](https://github.com/G6938/cnet/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/cnet.svg)](https://www.nuget.org/packages/cnet)
[![Downloads](https://img.shields.io/nuget/dt/cnet.svg)](https://www.nuget.org/packages/cnet)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/G6938/cnet/blob/main/LICENSE)
![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4.svg)
![Bot API 10.1](https://img.shields.io/badge/Bot%20API-10.1-2CA5E0.svg)

Telegram bot toolkit for .NET 9 — strongly typed handlers, built-in flood
control and automatic retry, sessions and FSM, media-group aggregation, long
polling, and hardened webhooks.

## Quick start

```csharp
var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddCnet(options => options.BotToken = builder.Configuration["BotToken"]!)
    .UseReplayGuard()
    .OnCommand("start", async cmd =>
    {
        var keyboard = Keyboard.Inline()
            .Row().Callback("Help", "help").Url("Site", "https://example.com")
            .Build();

        await cmd.ReplyAsync("<b>Hello!</b>", keyboard);
    })
    .OnCallback("help", async cb =>
    {
        await cb.AnswerAsync();
        await cb.EditTextAsync("Help text");
    })
    .OnMessage(async msg =>
    {
        await msg.TypingAsync();
        await msg.ReplyQuotedAsync("Echo: " + msg.Text);
    })
    .UsePolling();

await builder.Build().RunAsync();
```

For webhooks, add the `cnet.aspnetcore` package:

```csharp
builder.Services.AddCnetWebhook(options =>
{
    options.PublicUrl = "https://bot.example.com";
    options.Path = "/telegram/webhook";
    options.SecretToken = configuration["WebhookSecret"]!;
});

app.MapCnetWebhook();
```

## Highlights

- Typed turns: command, callback, message, and album handlers each receive a
  dedicated context with exactly the operations that interaction supports
- Automatic retry on 429 (`retry_after` respected) and transient network errors
- Proactive outbound throttle: 30 msg/s global, 1 msg/s per chat, on by default
- Sessions and FSM with `OnState(...)` routing and pluggable storage
- Media-group aggregation: whole albums arrive as one event
- Inbound per-user rate limiting, replay protection, global error hook
- Class-based handlers with constructor injection
- Bounded update queue with backpressure and graceful shutdown
- Wrappers for every common send and management operation, plus `ctx.Bot` for
  direct IntelliSense access to all 180+ Bot API 10.1 methods (rich messages,
  guardian bots, gifts, business accounts) with the same retry policy

## Documentation

- [Repository and full documentation](https://github.com/G6938/cnet)
- [Changelog](https://github.com/G6938/cnet/blob/main/CHANGELOG.md)
- [Contributing guide](https://github.com/G6938/cnet/blob/main/CONTRIBUTING.md)
- [Security policy](https://github.com/G6938/cnet/blob/main/SECURITY.md)

## License

Released under the [MIT License](https://github.com/G6938/cnet/blob/main/LICENSE).
Telegram is a trademark of Telegram FZ-LLC; this project is independent and
not affiliated with or endorsed by Telegram.
