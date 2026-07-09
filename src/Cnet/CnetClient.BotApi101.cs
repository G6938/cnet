using Telegram.Bot;
using Telegram.Bot.Types;

namespace Cnet;

public sealed partial class CnetClient
{
    public Task<Message> SendRichMessageAsync(long chatId, InputRichMessage richMessage, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(richMessage);
        return ExecuteAsync((bot, ct) => bot.SendRichMessage(chatId, richMessage, cancellationToken: ct), cancellationToken);
    }

    public Task AnswerJoinRequestQueryAsync(string joinRequestQueryId, string result, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(joinRequestQueryId);
        ArgumentException.ThrowIfNullOrEmpty(result);
        return ExecuteAsync(
            (bot, ct) => bot.AnswerChatJoinRequestQuery(joinRequestQueryId, result, ct),
            cancellationToken);
    }
}
