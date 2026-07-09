# cnet

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
    .OnMessage(async ctx => await ctx.ReplyAsync("Echo: " + ctx.Message.Text))
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
- Bounded update queue, configurable worker concurrency, graceful shutdown
- Longest-prefix callback routing, command parsing with `@BotName` support
- Middleware pipeline (`IUpdateMiddleware`) with built-in replay guard
- Fluent inline and reply keyboard builders
- Constant-time webhook secret validation

## License

MIT
