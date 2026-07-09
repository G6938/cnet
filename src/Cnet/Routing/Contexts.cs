using Telegram.Bot.Types;

namespace Cnet.Routing;

public class UpdateContext(Update update, CnetClient client, IServiceProvider services, CancellationToken cancellationToken)
{
    public Update Update { get; } = update;

    public CnetClient Client { get; } = client;

    public IServiceProvider Services { get; } = services;

    public CancellationToken CancellationToken { get; } = cancellationToken;

    public long? FromId =>
        Update.Message?.From?.Id
        ?? Update.EditedMessage?.From?.Id
        ?? Update.CallbackQuery?.From.Id
        ?? Update.InlineQuery?.From.Id
        ?? Update.MyChatMember?.From.Id;

    public string? LanguageCode =>
        Update.Message?.From?.LanguageCode
        ?? Update.EditedMessage?.From?.LanguageCode
        ?? Update.CallbackQuery?.From.LanguageCode
        ?? Update.InlineQuery?.From.LanguageCode;
}

public sealed class CommandContext(
    UpdateContext inner,
    Message message,
    string command,
    string arguments)
    : UpdateContext(inner.Update, inner.Client, inner.Services, inner.CancellationToken)
{
    public Message Message { get; } = message;

    public string Command { get; } = command;

    public string Arguments { get; } = arguments;

    public long ChatId => Message.Chat.Id;

    public long UserId => Message.From!.Id;

    public Task ReplyAsync(string text) => Client.SendTextAsync(ChatId, text, cancellationToken: CancellationToken);
}

public sealed class MessageContext(UpdateContext inner, Message message)
    : UpdateContext(inner.Update, inner.Client, inner.Services, inner.CancellationToken)
{
    public Message Message { get; } = message;

    public long ChatId => Message.Chat.Id;

    public long UserId => Message.From!.Id;

    public int? ReplyToMessageId => Message.ReplyToMessage?.MessageId;

    public Task ReplyAsync(string text) => Client.SendTextAsync(ChatId, text, cancellationToken: CancellationToken);
}

public sealed class CallbackContext(UpdateContext inner, CallbackQuery callbackQuery, string payload)
    : UpdateContext(inner.Update, inner.Client, inner.Services, inner.CancellationToken)
{
    public CallbackQuery CallbackQuery { get; } = callbackQuery;

    public string Data => CallbackQuery.Data ?? string.Empty;

    public string Payload { get; } = payload;

    public long UserId => CallbackQuery.From.Id;

    public long? ChatId => CallbackQuery.Message?.Chat.Id;

    public int? MessageId => CallbackQuery.Message?.MessageId;

    public Task AnswerAsync(string? text = null, bool showAlert = false)
        => Client.AnswerCallbackAsync(CallbackQuery.Id, text, showAlert, CancellationToken);
}
