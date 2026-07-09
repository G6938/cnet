using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cnet.Pipeline;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Cnet.AspNetCore;

public static class CnetWebhookExtensions
{
    private const string SecretTokenHeader = "X-Telegram-Bot-Api-Secret-Token";

    public static IServiceCollection AddCnetWebhook(this IServiceCollection services, Action<CnetWebhookOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddOptions<CnetWebhookOptions>()
            .Configure(configure)
            .Validate(options => !string.IsNullOrWhiteSpace(options.SecretToken), "CnetWebhookOptions.SecretToken is required.")
            .Validate(options => options.Path.StartsWith('/'), "CnetWebhookOptions.Path must start with '/'.")
            .Validate(
                options => !options.AutoRegister || !string.IsNullOrWhiteSpace(options.PublicUrl),
                "CnetWebhookOptions.PublicUrl is required when AutoRegister is enabled.")
            .ValidateOnStart();

        services.AddHostedService<WebhookRegistrationService>();
        return services;
    }

    public static IEndpointRouteBuilder MapCnetWebhook(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var options = endpoints.ServiceProvider.GetRequiredService<IOptions<CnetWebhookOptions>>().Value;
        endpoints.MapPost(options.Path, HandleAsync);
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        IOptions<CnetWebhookOptions> options,
        IUpdateChannel channel,
        CancellationToken cancellationToken)
    {
        var presented = context.Request.Headers[SecretTokenHeader].ToString();
        if (!SecretsMatch(presented, options.Value.SecretToken))
        {
            return Results.Unauthorized();
        }

        Update? update;
        try
        {
            update = await JsonSerializer
                .DeserializeAsync<Update>(context.Request.Body, JsonBotAPI.Options, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return Results.BadRequest();
        }

        if (update is null)
        {
            return Results.BadRequest();
        }

        try
        {
            await channel.EnqueueAsync(update, cancellationToken).ConfigureAwait(false);
            return Results.Ok();
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static bool SecretsMatch(string presented, string configured)
        => presented.Length > 0
            && configured.Length > 0
            && CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(presented),
                Encoding.UTF8.GetBytes(configured));
}
