using Telegram.Bot.Types;

namespace Cnet.Albums;

public interface IAlbumStore
{
    ValueTask AddAsync(Message message, TimeSpan flushDelay, CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<IReadOnlyList<Message>>> CollectDueAsync(CancellationToken cancellationToken = default);
}
