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

    public bool EnableOutboundThrottle { get; set; } = true;

    public int OutboundGlobalPerSecond { get; set; } = 30;

    public int OutboundPerChatIntervalMilliseconds { get; set; } = 1000;

    public int AlbumFlushDelayMilliseconds { get; set; } = 1500;
}
