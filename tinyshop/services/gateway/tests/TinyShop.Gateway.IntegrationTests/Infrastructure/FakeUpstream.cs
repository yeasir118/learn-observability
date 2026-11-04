using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace TinyShop.Gateway.IntegrationTests.Infrastructure;

/// <summary>
/// A real HTTP server on 127.0.0.1 and a random free port, standing in for an internal service.
/// Real, not a stub HttpMessageHandler: headers, framing and streaming only exist on a real
/// connection (TS-014, decision 10). Each test sets <see cref="Handler"/> to decide the answer,
/// and reads <see cref="LastRequest"/> to see what the gateway sent.
/// </summary>
public sealed class FakeUpstream : IAsyncDisposable
{
    private readonly WebApplication _app;

    private FakeUpstream(WebApplication app) => _app = app;

    /// <summary>How the upstream answers. Default: 200 with an empty JSON object.</summary>
    public Func<HttpContext, Task> Handler { get; set; } = context => context.Response.WriteAsJsonAsync(new { });

    /// <summary>The last request the upstream received, as it arrived.</summary>
    public ReceivedRequest? LastRequest { get; private set; }

    /// <summary>Where the gateway should send requests, e.g. http://127.0.0.1:53712/.</summary>
    public string BaseUrl { get; private set; } = "";

    public static async Task<FakeUpstream> StartAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");   // port 0: the OS picks a free one

        var app = builder.Build();
        var upstream = new FakeUpstream(app);
        app.Run(async context =>
        {
            using var reader = new StreamReader(context.Request.Body);
            upstream.LastRequest = new ReceivedRequest(
                context.Request.Method,
                context.Request.Path.Value ?? "",
                context.Request.QueryString.Value ?? "",
                context.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString(), StringComparer.OrdinalIgnoreCase),
                await reader.ReadToEndAsync());
            await upstream.Handler(context);
        });

        await app.StartAsync();
        upstream.BaseUrl = app.Services.GetRequiredService<IServer>()
            .Features.GetRequiredFeature<IServerAddressesFeature>().Addresses.Single() + "/";
        return upstream;
    }

    public ValueTask DisposeAsync() => _app.DisposeAsync();
}

/// <summary>A request as the upstream received it. Header names are case-insensitive.</summary>
public sealed record ReceivedRequest(
    string Method,
    string Path,
    string QueryString,
    IReadOnlyDictionary<string, string> Headers,
    string Body);
