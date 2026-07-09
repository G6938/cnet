using System.Text.Json;
using Cnet.Albums;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Cnet.Redis;

public sealed class RedisAlbumStore(IConnectionMultiplexer connection, IOptions<CnetRedisOptions> options)
    : IAlbumStore
{
    private const string ClaimScript = """
        local due = redis.call('ZRANGEBYSCORE', KEYS[1], 0, ARGV[1], 'LIMIT', 0, ARGV[2])
        if #due == 0 then return {} end
        for i = 1, #due do
            redis.call('ZREM', KEYS[1], due[i])
        end
        return due
        """;

    private static readonly JsonSerializerOptions SerializerOptions = JsonSerializerOptions.Default;

    public async ValueTask AddAsync(Message message, TimeSpan flushDelay, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var groupKey = message.Chat.Id + ":" + message.MediaGroupId;
        var database = connection.GetDatabase();
        var payload = JsonSerializer.Serialize(message, JsonBotAPI.Options);
        var dueAt = DateTimeOffset.UtcNow.Add(flushDelay).ToUnixTimeMilliseconds();

        var batch = database.CreateBatch();
        var push = batch.ListRightPushAsync(ItemsKey(groupKey), payload);
        var expire = batch.KeyExpireAsync(ItemsKey(groupKey), TimeSpan.FromMinutes(5));
        var schedule = batch.SortedSetAddAsync(DueKey(), groupKey, dueAt);
        batch.Execute();

        await Task.WhenAll(push, expire, schedule).ConfigureAwait(false);
    }

    public async ValueTask<IReadOnlyList<IReadOnlyList<Message>>> CollectDueAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var database = connection.GetDatabase();
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var claimed = (RedisValue[]?)await database.ScriptEvaluateAsync(
            ClaimScript,
            [DueKey()],
            [now, 16]).ConfigureAwait(false);

        if (claimed is null || claimed.Length == 0)
        {
            return [];
        }

        var albums = new List<IReadOnlyList<Message>>(claimed.Length);
        foreach (var groupKey in claimed)
        {
            var itemsKey = ItemsKey(groupKey.ToString());
            var payloads = await database.ListRangeAsync(itemsKey).ConfigureAwait(false);
            await database.KeyDeleteAsync(itemsKey).ConfigureAwait(false);

            var messages = payloads
                .Select(payload => JsonSerializer.Deserialize<Message>(payload.ToString(), JsonBotAPI.Options))
                .Where(message => message is not null)
                .Select(message => message!)
                .OrderBy(message => message.Id)
                .ToList();

            if (messages.Count > 0)
            {
                albums.Add(messages);
            }
        }

        return albums;
    }

    private string ItemsKey(string groupKey) => options.Value.KeyPrefix + ":album:items:" + groupKey;

    private string DueKey() => options.Value.KeyPrefix + ":album:due";
}
