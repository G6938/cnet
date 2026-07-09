using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Cnet;

public sealed partial class CnetClient
{
    public async Task<int> SendVideoAsync(long chatId, string fileIdOrUrl, string? caption = null, ReplyMarkup? keyboard = null, CancellationToken cancellationToken = default)
    {
        await throttle.WaitAsync(chatId, cancellationToken).ConfigureAwait(false);
        var message = await ExecuteAsync(
            (bot, ct) => bot.SendVideo(chatId, InputFile.FromString(fileIdOrUrl), caption: caption, parseMode: ParseMode.Html, replyMarkup: keyboard, cancellationToken: ct),
            cancellationToken).ConfigureAwait(false);
        return message.MessageId;
    }

    public async Task<int> SendAudioAsync(long chatId, string fileIdOrUrl, string? caption = null, CancellationToken cancellationToken = default)
    {
        await throttle.WaitAsync(chatId, cancellationToken).ConfigureAwait(false);
        var message = await ExecuteAsync(
            (bot, ct) => bot.SendAudio(chatId, InputFile.FromString(fileIdOrUrl), caption: caption, parseMode: ParseMode.Html, cancellationToken: ct),
            cancellationToken).ConfigureAwait(false);
        return message.MessageId;
    }

    public async Task<int> SendVoiceAsync(long chatId, string fileIdOrUrl, string? caption = null, CancellationToken cancellationToken = default)
    {
        await throttle.WaitAsync(chatId, cancellationToken).ConfigureAwait(false);
        var message = await ExecuteAsync(
            (bot, ct) => bot.SendVoice(chatId, InputFile.FromString(fileIdOrUrl), caption: caption, parseMode: ParseMode.Html, cancellationToken: ct),
            cancellationToken).ConfigureAwait(false);
        return message.MessageId;
    }

    public async Task<int> SendAnimationAsync(long chatId, string fileIdOrUrl, string? caption = null, CancellationToken cancellationToken = default)
    {
        await throttle.WaitAsync(chatId, cancellationToken).ConfigureAwait(false);
        var message = await ExecuteAsync(
            (bot, ct) => bot.SendAnimation(chatId, InputFile.FromString(fileIdOrUrl), caption: caption, parseMode: ParseMode.Html, cancellationToken: ct),
            cancellationToken).ConfigureAwait(false);
        return message.MessageId;
    }

    public async Task<int> SendVideoNoteAsync(long chatId, string fileIdOrUrl, CancellationToken cancellationToken = default)
    {
        await throttle.WaitAsync(chatId, cancellationToken).ConfigureAwait(false);
        var message = await ExecuteAsync(
            (bot, ct) => bot.SendVideoNote(chatId, InputFile.FromString(fileIdOrUrl), cancellationToken: ct),
            cancellationToken).ConfigureAwait(false);
        return message.MessageId;
    }

    public async Task<int> SendLocationAsync(long chatId, double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        await throttle.WaitAsync(chatId, cancellationToken).ConfigureAwait(false);
        var message = await ExecuteAsync(
            (bot, ct) => bot.SendLocation(chatId, latitude, longitude, cancellationToken: ct),
            cancellationToken).ConfigureAwait(false);
        return message.MessageId;
    }

    public async Task<int> SendContactAsync(long chatId, string phoneNumber, string firstName, string? lastName = null, CancellationToken cancellationToken = default)
    {
        await throttle.WaitAsync(chatId, cancellationToken).ConfigureAwait(false);
        var message = await ExecuteAsync(
            (bot, ct) => bot.SendContact(chatId, phoneNumber, firstName, lastName: lastName, cancellationToken: ct),
            cancellationToken).ConfigureAwait(false);
        return message.MessageId;
    }

    public async Task<int> SendPollAsync(long chatId, string question, IEnumerable<string> options, bool anonymous = true, bool multipleAnswers = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        await throttle.WaitAsync(chatId, cancellationToken).ConfigureAwait(false);
        var message = await ExecuteAsync(
            (bot, ct) => bot.SendPoll(chatId, question, options.Select(option => new InputPollOption(option)), isAnonymous: anonymous, allowsMultipleAnswers: multipleAnswers, cancellationToken: ct),
            cancellationToken).ConfigureAwait(false);
        return message.MessageId;
    }

    public async Task<int> SendDiceAsync(long chatId, string emoji = "🎲", CancellationToken cancellationToken = default)
    {
        await throttle.WaitAsync(chatId, cancellationToken).ConfigureAwait(false);
        var message = await ExecuteAsync(
            (bot, ct) => bot.SendDice(chatId, emoji: emoji, cancellationToken: ct),
            cancellationToken).ConfigureAwait(false);
        return message.MessageId;
    }

    public Task EditCaptionAsync(long chatId, int messageId, string caption, InlineKeyboardMarkup? keyboard = null, CancellationToken cancellationToken = default)
        => ExecuteAsync(
            (bot, ct) => bot.EditMessageCaption(chatId, messageId, caption: caption, parseMode: ParseMode.Html, replyMarkup: keyboard, cancellationToken: ct),
            cancellationToken);

    public Task EditReplyMarkupAsync(long chatId, int messageId, InlineKeyboardMarkup? keyboard, CancellationToken cancellationToken = default)
        => ExecuteAsync(
            (bot, ct) => bot.EditMessageReplyMarkup(chatId, messageId, replyMarkup: keyboard, cancellationToken: ct),
            cancellationToken);

    public Task PinAsync(long chatId, int messageId, bool notify = false, CancellationToken cancellationToken = default)
        => ExecuteAsync((bot, ct) => bot.PinChatMessage(chatId, messageId, disableNotification: !notify, cancellationToken: ct), cancellationToken);

    public Task UnpinAsync(long chatId, int messageId, CancellationToken cancellationToken = default)
        => ExecuteAsync((bot, ct) => bot.UnpinChatMessage(chatId, messageId, cancellationToken: ct), cancellationToken);

    public Task BanAsync(long chatId, long userId, CancellationToken cancellationToken = default)
        => ExecuteAsync((bot, ct) => bot.BanChatMember(chatId, userId, cancellationToken: ct), cancellationToken);

    public Task UnbanAsync(long chatId, long userId, CancellationToken cancellationToken = default)
        => ExecuteAsync((bot, ct) => bot.UnbanChatMember(chatId, userId, cancellationToken: ct), cancellationToken);

    public Task<ChatFullInfo> GetChatAsync(long chatId, CancellationToken cancellationToken = default)
        => ExecuteAsync((bot, ct) => bot.GetChat(chatId, ct), cancellationToken);

    public Task<ChatMember> GetChatMemberAsync(long chatId, long userId, CancellationToken cancellationToken = default)
        => ExecuteAsync((bot, ct) => bot.GetChatMember(chatId, userId, ct), cancellationToken);

    public async Task<int> SendMediaGroupAsync(long chatId, IEnumerable<IAlbumInputMedia> media, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(media);
        await throttle.WaitAsync(chatId, cancellationToken).ConfigureAwait(false);
        var messages = await ExecuteAsync(
            (bot, ct) => bot.SendMediaGroup(chatId, media, cancellationToken: ct),
            cancellationToken).ConfigureAwait(false);
        return messages.Length;
    }
}
