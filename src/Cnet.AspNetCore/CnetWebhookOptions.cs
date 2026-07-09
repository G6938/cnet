namespace Cnet.AspNetCore;

public sealed class CnetWebhookOptions
{
    public string Path { get; set; } = "/telegram/webhook";

    public string SecretToken { get; set; } = string.Empty;

    public string? PublicUrl { get; set; }

    public bool AutoRegister { get; set; } = true;

    public int MaxConnections { get; set; } = 40;
}
