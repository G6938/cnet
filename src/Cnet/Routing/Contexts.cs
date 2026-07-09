using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

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

public abstract class MessageContextBase(UpdateContext inner, Message message)
    : UpdateContext(inner.Update, inner.Client, inner.Services, inner.CancellationToken)
{
    public Message Message { get; } = message;

    public long ChatId => Message.Chat.Id;

    public long UserId => Message.From!.Id;

    public User From => Message.From!;

    public int MessageId => Message.Id;

    public string? Text => Message.Text ?? Message.Caption;

    public int? ReplyToMessageId => Message.ReplyToMessage?.MessageId;

    public Task<int> ReplyAsync(string text, ReplyMarkup? keyboard = null)
        => Client.SendTextAsync(ChatId, text, keyboard, cancellationToken: CancellationToken);

    public Task<int> ReplyQuotedAsync(string text, ReplyMarkup? keyboard = null)
        => Client.SendTextAsync(ChatId, text, keyboard, MessageId, cancellationToken: CancellationToken);

    public Task<int> ReplyWithPhotoAsync(string fileIdOrUrl, string? caption = null, ReplyMarkup? keyboard = null)
        => Client.SendPhotoAsync(ChatId, fileIdOrUrl, caption, keyboard, CancellationToken);

    public Task<int> ReplyWithDocumentAsync(string fileIdOrUrl, string? caption = null)
        => Client.SendDocumentAsync(ChatId, fileIdOrUrl, caption, CancellationToken);

    public Task<int> ReplyWithStickerAsync(string stickerFileId)
        => Client.SendStickerAsync(ChatId, stickerFileId, CancellationToken);

    public Task ReactAsync(string emoji = "\U0001F44D")
        => Client.ReactAsync(ChatId, MessageId, emoji, CancellationToken);

    public Task DeleteAsync()
        => Client.DeleteMessageAsync(ChatId, MessageId, CancellationToken);

    public Task<int> CopyToAsync(long chatId, int? replyToMessageId = null, InlineKeyboardMarkup? keyboard = null)
        => Client.CopyAsync(chatId, ChatId, MessageId, replyToMessageId, keyboard, CancellationToken);

    public Task<int> ForwardToAsync(long chatId)
        => Client.ForwardAsync(chatId, ChatId, MessageId, CancellationToken);

    public Task TypingAsync()
        => Client.SendChatActionAsync(ChatId, ChatAction.Typing, CancellationToken);
}

public sealed class CommandContext(UpdateContext inner, Message message, string command, string arguments)
    : MessageContextBase(inner, message)
{
    public string Command { get; } = command;

    public string Arguments { get; } = arguments;
}

public sealed class MessageContext(UpdateContext inner, Message message)
    : MessageContextBase(inner, message);

public sealed class CallbackContext(UpdateContext inner, CallbackQuery callbackQuery, string payload)
    : UpdateContext(inner.Update, inner.Client, inner.Services, inner.CancellationToken)
{
    public CallbackQuery CallbackQuery { get; } = callbackQuery;

    public string Data => CallbackQuery.Data ?? string.Empty;

    public string Payload { get; } = payload;

    public long UserId => CallbackQuery.From.Id;

    public User From => CallbackQuery.From;

    public long? ChatId => CallbackQuery.Message?.Chat.Id;

    public int? MessageId => CallbackQuery.Message?.MessageId;

    public Task AnswerAsync(string? text = null, bool showAlert = false)
        => Client.AnswerCallbackAsync(CallbackQuery.Id, text, showAlert, CancellationToken);

    public Task AlertAsync(string text)
        => AnswerAsync(text, showAlert: true);

    public Task EditTextAsync(string text, InlineKeyboardMarkup? keyboard = null)
        => Client.EditTextAsync(RequireChatId(), RequireMessageId(), text, keyboard, cancellationToken: CancellationToken);

    public Task DeleteMessageAsync()
        => Client.DeleteMessageAsync(RequireChatId(), RequireMessageId(), CancellationToken);

    public Task<int> ReplyAsync(string text, ReplyMarkup? keyboard = null)
        => Client.SendTextAsync(ChatId ?? UserId, text, keyboard, cancellationToken: CancellationToken);

    private long RequireChatId()
        => ChatId ?? throw new InvalidOperationException("The callback has no accessible message to act on.");

    private int RequireMessageId()
        => MessageId ?? throw new InvalidOperationException("The callback has no accessible message to act on.");
}
