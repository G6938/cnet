using System.Net;
using System.Text;
using Cnet.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Cnet.AspNetCore.Tests;

public sealed class WebhookEndpointTests : IAsyncLifetime
{
    private const string Secret = "webhook-secret-token";
    private const string Path = "/tg/webhook";

    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.WebHost.UseTestServer();

        builder.Services.AddCnet(options => options.BotToken = "1000000000:test");
        builder.Services.AddCnetWebhook(options =>
        {
            options.Path = Path;
            options.SecretToken = Secret;
            options.AutoRegister = false;
        });

        _app = builder.Build();
        _app.MapCnetWebhook();
        await _app.StartAsync();
        _client = _app.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }

    private static HttpRequestMessage Request(string body, string? secret)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, Path)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        if (secret is not null)
        {
            request.Headers.Add("X-Telegram-Bot-Api-Secret-Token", secret);
        }

        return request;
    }

    [Fact]
    public async Task MissingSecret_Returns401()
    {
        using var request = Request("""{"update_id":1}""", null);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WrongSecret_Returns401()
    {
        using var request = Request("""{"update_id":1}""", "wrong");
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ValidUpdate_Returns200()
    {
        using var request = Request("""{"update_id":42}""", Secret);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task MalformedBody_Returns400()
    {
        using var request = Request("not json", Secret);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
