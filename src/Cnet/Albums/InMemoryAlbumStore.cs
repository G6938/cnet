using System.Collections.Concurrent;
using Telegram.Bot.Types;

namespace Cnet.Albums;

public sealed class InMemoryAlbumStore : IAlbumStore
{
    private sealed class Pending
    {
        public List<Message> Messages { get; } = [];

        public long DueAtTicks { get; set; }
    }

    private readonly ConcurrentDictionary<string, Pending> _groups = new(StringComparer.Ordinal);

    public ValueTask AddAsync(Message message, TimeSpan flushDelay, CancellationToken cancellationToken = default)
    {
        var groupKey = message.Chat.Id + ":" + message.MediaGroupId;
        var pending = _groups.GetOrAdd(groupKey, _ => new Pending());

        lock (pending)
        {
            pending.Messages.Add(message);
            pending.DueAtTicks = Environment.TickCount64 + (long)flushDelay.TotalMilliseconds;
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask<IReadOnlyList<IReadOnlyList<Message>>> CollectDueAsync(CancellationToken cancellationToken = default)
    {
        var now = Environment.TickCount64;
        var due = new List<IReadOnlyList<Message>>();

        foreach (var pair in _groups)
        {
            lock (pair.Value)
            {
                if (pair.Value.Messages.Count > 0 && now >= pair.Value.DueAtTicks
                    && _groups.TryRemove(pair.Key, out _))
                {
                    due.Add([.. pair.Value.Messages.OrderBy(message => message.Id)]);
                }
            }
        }

        return ValueTask.FromResult<IReadOnlyList<IReadOnlyList<Message>>>(due);
    }
}
