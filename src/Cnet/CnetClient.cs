using Cnet.Throttling;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Cnet;

public sealed partial class CnetClient(ITelegramBotClient raw, IOutboundThrottle throttle, IOptions<CnetOptions> options)
{
    private readonly int _maxAttempts = options.Value.MaxSendAttempts;
    private User? _me;

    public ITelegramBotClient Raw { get; } = raw;

    public Task<T> ExecuteAsync<T>(Func<ITelegramBotClient, CancellationToken, Task<T>> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        return RetryExecutor.ExecuteAsync(ct => action(Raw, ct), _maxAttempts, cancellationToken);
    }

    public Task ExecuteAsync(Func<ITelegramBotClient, CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        return RetryExecutor.ExecuteAsync(ct => action(Raw, ct), _maxAttempts, cancellationToken);
    }

    public async Task<User> GetMeAsync(CancellationToken cancellationToken = default)
        => _me ??= await ExecuteAsync((bot, ct) => bot.GetMe(ct), cancellationToken).ConfigureAwait(false);

    public async Task<string> GetUsernameAsync(CancellationToken cancellationToken = default)
    {
        var me = await GetMeAsync(cancellationToken).ConfigureAwait(false);
        return me.Username ?? throw new InvalidOperationException("The bot has no username.");
    }

    public async Task<string> BuildDeepLinkAsync(string payload, CancellationToken cancellationToken = default)
        => "https://t.me/" + await GetUsernameAsync(cancellationToken).ConfigureAwait(false) + "?start=" + payload;

    public async Task<int> SendTextAsync(
        long chatId,
        string text,
        ReplyMarkup? keyboard = null,
        int? replyToMessageId = null,
        ParseMode parseMode = ParseMode.Html,
        bool disableLinkPreview = true,
        CancellationToken cancellationToken = default)
    {
        await throttle.WaitAsync(chatId, cancellationToken).ConfigureAwait(false);
        var message = await ExecuteAsync(
            (bot, ct) => bot.SendMessage(
                chatId,
                text,
                parseMode: parseMode,
                replyParameters: ToReplyParameters(replyToMessageId),
                linkPreviewOptions: disableLinkPreview ? LinkPreviewOptions.Disabled : null,
                replyMarkup: keyboard,
                cancellationToken: ct),
            cancellationToken).ConfigureAwait(false);

        return message.MessageId;
    }

    public Task EditTextAsync(
        long chatId,
        int messageId,
        string text,
        InlineKeyboardMarkup? keyboard = null,
        ParseMode parseMode = ParseMode.Html,
        CancellationToken cancellationToken = default)
        => EditTextCoreAsync(chatId, messageId, text, keyboard, parseMode, cancellationToken);

    private async Task EditTextCoreAsync(
        long chatId,
        int messageId,
        string text,
        InlineKeyboardMarkup? keyboard,
        ParseMode parseMode,
        CancellationToken cancellationToken)
    {
        await throttle.WaitAsync(chatId, cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(
            (bot, ct) => bot.EditMessageText(
                chatId,
                messageId,
                text,
                parseMode: parseMode,
                linkPreviewOptions: LinkPreviewOptions.Disabled,
                replyMarkup: keyboard,
                cancellationToken: ct),
            cancellationToken).ConfigureAwait(false);
    }

    public Task DeleteMessageAsync(long chatId, int messageId, CancellationToken cancellationToken = default)
        => ExecuteAsync((bot, ct) => bot.DeleteMessage(chatId, messageId, ct), cancellationToken);

    public async Task<int> SendStickerAsync(long chatId, string stickerFileId, CancellationToken cancellationToken = default)
    {
        await throttle.WaitAsync(chatId, cancellationToken).ConfigureAwait(false);
        var message = await ExecuteAsync(
            (bot, ct) => bot.SendSticker(chatId, InputFile.FromFileId(stickerFileId), cancellationToken: ct),
            cancellationToken).ConfigureAwait(false);
        return message.MessageId;
    }

    public async Task<int> SendPhotoAsync(
        long chatId,
        string fileIdOrUrl,
        string? caption = null,
        ReplyMarkup? keyboard = null,
        CancellationToken cancellationToken = default)
    {
        await throttle.WaitAsync(chatId, cancellationToken).ConfigureAwait(false);
        var message = await ExecuteAsync(
            (bot, ct) => bot.SendPhoto(
                chatId,
                InputFile.FromString(fileIdOrUrl),
                caption: caption,
                parseMode: ParseMode.Html,
                replyMarkup: keyboard,
                cancellationToken: ct),
            cancellationToken).ConfigureAwait(false);
        return message.MessageId;
    }

    public async Task<int> SendDocumentAsync(
        long chatId,
        string fileIdOrUrl,
        string? caption = null,
        CancellationToken cancellationToken = default)
    {
        await throttle.WaitAsync(chatId, cancellationToken).ConfigureAwait(false);
        var message = await ExecuteAsync(
            (bot, ct) => bot.SendDocument(
                chatId,
                InputFile.FromString(fileIdOrUrl),
                caption: caption,
                parseMode: ParseMode.Html,
                cancellationToken: ct),
            cancellationToken).ConfigureAwait(false);
        return message.MessageId;
    }

    public async Task<int> CopyAsync(
        long toChatId,
        long fromChatId,
        int messageId,
        int? replyToMessageId = null,
        InlineKeyboardMarkup? keyboard = null,
        CancellationToken cancellationToken = default)
    {
        await throttle.WaitAsync(toChatId, cancellationToken).ConfigureAwait(false);
        var copied = await ExecuteAsync(
            (bot, ct) => bot.CopyMessage(
                toChatId,
                fromChatId,
                messageId,
                replyParameters: ToReplyParameters(replyToMessageId),
                replyMarkup: keyboard,
                cancellationToken: ct),
            cancellationToken).ConfigureAwait(false);
        return copied.Id;
    }

    public async Task<IReadOnlyList<int>> CopyManyAsync(
        long toChatId,
        long fromChatId,
        IReadOnlyList<int> messageIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messageIds);

        await throttle.WaitAsync(toChatId, cancellationToken).ConfigureAwait(false);
        var copied = await ExecuteAsync(
            (bot, ct) => bot.CopyMessages(toChatId, fromChatId, [.. messageIds], cancellationToken: ct),
            cancellationToken).ConfigureAwait(false);
        return [.. copied.Select(messageId => messageId.Id)];
    }

    public async Task AnswerCallbackAsync(
        string callbackQueryId,
        string? text = null,
        bool showAlert = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await Raw.AnswerCallbackQuery(callbackQueryId, text, showAlert, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ApiRequestException)
        {
        }
    }

    public async Task ReactAsync(long chatId, int messageId, string emoji, CancellationToken cancellationToken = default)
    {
        try
        {
            await Raw.SetMessageReaction(
                chatId,
                messageId,
                [new ReactionTypeEmoji { Emoji = emoji }],
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (ApiRequestException)
        {
        }
    }

    public Task SetCommandsAsync(IEnumerable<(string Command, string Description)> commands, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(commands);

        return ExecuteAsync(
            (bot, ct) => bot.SetMyCommands(
                commands.Select(pair => new BotCommand { Command = pair.Command, Description = pair.Description }),
                cancellationToken: ct),
            cancellationToken);
    }

    public async Task<int> ForwardAsync(long toChatId, long fromChatId, int messageId, CancellationToken cancellationToken = default)
    {
        await throttle.WaitAsync(toChatId, cancellationToken).ConfigureAwait(false);
        var forwarded = await ExecuteAsync(
            (bot, ct) => bot.ForwardMessage(toChatId, fromChatId, messageId, cancellationToken: ct),
            cancellationToken).ConfigureAwait(false);
        return forwarded.MessageId;
    }

    public Task SendChatActionAsync(long chatId, ChatAction action = ChatAction.Typing, CancellationToken cancellationToken = default)
        => ExecuteAsync((bot, ct) => bot.SendChatAction(chatId, action, cancellationToken: ct), cancellationToken);

    public async Task DownloadFileAsync(string fileId, Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileId);
        ArgumentNullException.ThrowIfNull(destination);

        var file = await ExecuteAsync((bot, ct) => bot.GetFile(fileId, ct), cancellationToken).ConfigureAwait(false);
        await Raw.DownloadFile(file.FilePath!, destination, cancellationToken).ConfigureAwait(false);
    }

    private static ReplyParameters? ToReplyParameters(int? replyToMessageId)
        => replyToMessageId is int id
            ? new ReplyParameters { MessageId = id, AllowSendingWithoutReply = true }
            : null;
}
