using Cnet.Pipeline;
using Cnet.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Cnet.Testing;

public sealed class BotTestHarness : IAsyncDisposable
{
    private readonly ServiceProvider _provider;
    private int _nextUpdateId = 1;
    private int _nextMessageId = 1;

    public BotTestHarness(Action<IServiceCollection> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var services = new ServiceCollection();
        services.AddLogging();
        configure(services);
        services.RemoveAll<ITelegramBotClient>();
        services.AddSingleton<ITelegramBotClient>(Bot);
        _provider = services.BuildServiceProvider();
    }

    public FakeBotClient Bot { get; } = new();

    public IServiceProvider Services => _provider;

    public Task SendCommandAsync(string command, string arguments = "", long chatId = 100, long userId = 100)
    {
        var text = arguments.Length > 0 ? "/" + command.TrimStart('/') + " " + arguments : "/" + command.TrimStart('/');
        return SendTextAsync(text, chatId, userId);
    }

    public Task SendTextAsync(string text, long chatId = 100, long userId = 100, int? replyToMessageId = null)
    {
        var message = new Message
        {
            Id = Interlocked.Increment(ref _nextMessageId),
            Date = DateTime.UtcNow,
            Chat = new Chat { Id = chatId, Type = Telegram.Bot.Types.Enums.ChatType.Private },
            From = new User { Id = userId, FirstName = "Test" },
            Text = text,
            ReplyToMessage = replyToMessageId is int id
                ? new Message { Id = id, Date = DateTime.UtcNow, Chat = new Chat { Id = chatId } }
                : null,
        };

        return DispatchAsync(new Update { Id = Interlocked.Increment(ref _nextUpdateId), Message = message });
    }

    public Task SendCallbackAsync(string data, long chatId = 100, long userId = 100, int messageId = 1)
    {
        var callbackQuery = new CallbackQuery
        {
            Id = Guid.NewGuid().ToString("N"),
            From = new User { Id = userId, FirstName = "Test" },
            Data = data,
            ChatInstance = "ci",
            Message = new Message
            {
                Id = messageId,
                Date = DateTime.UtcNow,
                Chat = new Chat { Id = chatId, Type = Telegram.Bot.Types.Enums.ChatType.Private },
            },
        };

        return DispatchAsync(new Update { Id = Interlocked.Increment(ref _nextUpdateId), CallbackQuery = callbackQuery });
    }

    public async Task DispatchAsync(Update update)
    {
        ArgumentNullException.ThrowIfNull(update);

        await using var scope = _provider.CreateAsyncScope();
        var client = scope.ServiceProvider.GetRequiredService<CnetClient>();
        var context = new UpdateContext(update, client, scope.ServiceProvider, CancellationToken.None);
        var pipeline = scope.ServiceProvider.GetRequiredService<UpdatePipeline>();
        await pipeline.ExecuteAsync(context).ConfigureAwait(false);
    }

    public ValueTask DisposeAsync() => _provider.DisposeAsync();
}
