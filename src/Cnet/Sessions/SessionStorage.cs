using System.Collections.Concurrent;
using System.Text.Json;

namespace Cnet.Sessions;

public interface ISessionStorage
{
    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);

    Task SetAsync(string key, string value, TimeSpan? lifetime = null, CancellationToken cancellationToken = default);

    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}

public sealed class InMemorySessionStorage : ISessionStorage
{
    private readonly ConcurrentDictionary<string, (string Value, long ExpiresAt)> _entries = new(StringComparer.Ordinal);

    public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        if (_entries.TryGetValue(key, out var entry))
        {
            if (entry.ExpiresAt == 0 || entry.ExpiresAt > Environment.TickCount64)
            {
                return Task.FromResult<string?>(entry.Value);
            }

            _entries.TryRemove(key, out _);
        }

        return Task.FromResult<string?>(null);
    }

    public Task SetAsync(string key, string value, TimeSpan? lifetime = null, CancellationToken cancellationToken = default)
    {
        var expiresAt = lifetime is TimeSpan ttl ? Environment.TickCount64 + (long)ttl.TotalMilliseconds : 0;
        _entries[key] = (value, expiresAt);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _entries.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}

public sealed class Session(ISessionStorage storage, string key, CancellationToken cancellationToken)
{
    private static readonly JsonSerializerOptions SerializerOptions = JsonSerializerOptions.Default;
    private static readonly TimeSpan DefaultLifetime = TimeSpan.FromHours(48);

    private sealed class SessionDocument
    {
        public string? State { get; set; }

        public Dictionary<string, JsonElement> Data { get; set; } = new(StringComparer.Ordinal);
    }

    public async Task<string?> GetStateAsync()
    {
        var document = await LoadAsync().ConfigureAwait(false);
        return document.State;
    }

    public async Task SetStateAsync(string? state)
    {
        var document = await LoadAsync().ConfigureAwait(false);
        document.State = state;
        await SaveAsync(document).ConfigureAwait(false);
    }

    public async Task<T?> GetAsync<T>(string field)
    {
        var document = await LoadAsync().ConfigureAwait(false);
        return document.Data.TryGetValue(field, out var element)
            ? element.Deserialize<T>(SerializerOptions)
            : default;
    }

    public async Task SetAsync<T>(string field, T value)
    {
        var document = await LoadAsync().ConfigureAwait(false);
        document.Data[field] = JsonSerializer.SerializeToElement(value, SerializerOptions);
        await SaveAsync(document).ConfigureAwait(false);
    }

    public Task ClearAsync() => storage.RemoveAsync(key, cancellationToken);

    private async Task<SessionDocument> LoadAsync()
    {
        var payload = await storage.GetAsync(key, cancellationToken).ConfigureAwait(false);
        return payload is null
            ? new SessionDocument()
            : JsonSerializer.Deserialize<SessionDocument>(payload, SerializerOptions) ?? new SessionDocument();
    }

    private Task SaveAsync(SessionDocument document)
        => storage.SetAsync(key, JsonSerializer.Serialize(document, SerializerOptions), DefaultLifetime, cancellationToken);
}
