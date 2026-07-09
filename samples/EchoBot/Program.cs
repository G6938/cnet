using Cnet.DependencyInjection;
using Cnet.Keyboards;
using Cnet.Sessions;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddCnet(options => options.BotToken = builder.Configuration["BotToken"]
        ?? throw new InvalidOperationException("Set BotToken via user secrets or the BotToken environment variable."))
    .UseReplayGuard()
    .UseRateLimit(20)
    .OnCommand("start", async cmd =>
    {
        var keyboard = Keyboard.Inline()
            .Row().Callback("Say hi", "hi").Url("Docs", "https://github.com/G6938/cnet")
            .Build();

        await cmd.ReplyAsync("<b>Welcome to the echo bot.</b>\nSend me anything, or /form to try a conversation.", keyboard);
    })
    .OnCommand("form", async cmd =>
    {
        await cmd.Session().SetStateAsync("await_name");
        await cmd.ReplyAsync("What is your name?");
    })
    .OnState("await_name", async msg =>
    {
        await msg.Session().SetAsync("name", msg.Text);
        await msg.Session().SetStateAsync("await_color");
        await msg.ReplyAsync("What is your favorite color?");
    })
    .OnState("await_color", async msg =>
    {
        var name = await msg.Session().GetAsync<string>("name");
        await msg.Session().ClearAsync();
        await msg.ReplyAsync($"Nice to meet you, {name}. {msg.Text} is a great color!");
    })
    .OnCallback("hi", async cb =>
    {
        await cb.AnswerAsync("Hi!");
        await cb.ReplyAsync("👋");
    })
    .OnMessage(async msg =>
    {
        await msg.TypingAsync();
        await msg.ReplyQuotedAsync("Echo: " + msg.Text);
    })
    .UsePolling();

await builder.Build().RunAsync();
