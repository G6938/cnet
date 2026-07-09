using Telegram.Bot.Types.Enums;

namespace Cnet;

public sealed class CnetOptions
{
    public string BotToken { get; set; } = string.Empty;

    public string? ApiBaseUrl { get; set; }

    public int UpdateQueueCapacity { get; set; } = 10000;

    public int WorkerConcurrency { get; set; } = 8;

    public int MaxSendAttempts { get; set; } = 3;

    public int PollingTimeoutSeconds { get; set; } = 50;

    public UpdateType[]? AllowedUpdates { get; set; }

    public bool DropPendingUpdates { get; set; }
}
