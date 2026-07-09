namespace Cnet.Redis;

public sealed class CnetRedisOptions
{
    public string ConnectionString { get; set; } = string.Empty;

    public string KeyPrefix { get; set; } = "cnet";

    public string StreamName { get; set; } = "cnet:updates";

    public string ConsumerGroup { get; set; } = "cnet-workers";

    public int StreamMaxLength { get; set; } = 100000;

    public TimeSpan ReplayWindow { get; set; } = TimeSpan.FromMinutes(15);

    public TimeSpan SessionLifetime { get; set; } = TimeSpan.FromHours(48);

    public TimeSpan PendingClaimIdle { get; set; } = TimeSpan.FromMinutes(1);
}
