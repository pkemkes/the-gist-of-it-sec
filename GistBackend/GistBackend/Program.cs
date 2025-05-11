using System.Net;
using GistBackend.Handler;
using GistBackend.Handler.ChromaDbHandler;
using GistBackend.Handler.GoogleSearchHandler;
using GistBackend.Handler.MariaDbHandler;
using GistBackend.Handler.OpenAiHandler;
using GistBackend.Services;
using GistBackend.Utils;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;
using Prometheus;
using Serilog;

namespace GistBackend;

public static class Program
{
    public const string RetryingHttpClientName = "WithRetry";

    public static async Task Main(string[] args)
    {
        const string gistMariaDbHandlerOptionsName = $"Gist{nameof(MariaDbHandlerOptions)}";
        const string recapMariaDbHandlerOptionsName = $"Recap{nameof(MariaDbHandlerOptions)}";
        const string cleanupMariaDbHandlerOptionsName = $"Cleanup{nameof(MariaDbHandlerOptions)}";
        const string dummyUserAgent = "Mozilla/5.0 (X11; Ubuntu; Linux x86_64; rv:131.0) Gecko/20100101 Firefox/131.0";

        var builder = Host.CreateDefaultBuilder(args);
        var host = builder.UseSerilog((_, _, configuration) => {
                configuration
                    .Enrich.FromLogContext()
                    .WriteTo.Console(new Serilog.Formatting.Json.JsonFormatter());
            })
            .ConfigureServices((context, services) => {
                // services.Configure<MariaDbHandlerOptions>(gistMariaDbHandlerOptionsName,
                //     context.Configuration.GetSection(gistMariaDbHandlerOptionsName));
                // services.Configure<MariaDbHandlerOptions>(recapMariaDbHandlerOptionsName,
                //     context.Configuration.GetSection(recapMariaDbHandlerOptionsName));
                services.Configure<EmbeddingClientHandlerOptions>(
                    context.Configuration.GetSection(nameof(EmbeddingClientHandlerOptions)));
                services.Configure<ChatClientHandlerOptions>(
                    context.Configuration.GetSection(nameof(ChatClientHandlerOptions)));
                services.Configure<ChromaDbHandlerOptions>(
                    context.Configuration.GetSection(nameof(ChromaDbHandlerOptions)));
                services.Configure<CustomSearchApiHandlerOptions>(
                    context.Configuration.GetSection(nameof(CustomSearchApiHandlerOptions)));
                services.Configure<CleanupServiceOptions>(
                    context.Configuration.GetSection(nameof(CleanupServiceOptions)));

                services.AddHttpClient(RetryingHttpClientName)
                    .ConfigureHttpClient(client => client.DefaultRequestHeaders.UserAgent.ParseAdd(dummyUserAgent))
                    .AddStandardResilienceHandler(options => {
                        options.Retry.BackoffType = DelayBackoffType.Exponential;
                    });

                services.AddTransient<IRssFeedHandler, RssFeedHandler>();
                services.AddTransient<IRssEntryHandler, RssEntryHandler>();
                services.AddTransient<IMariaDbHandler, MariaDbHandler>();
                services.AddTransient<IEmbeddingClientHandler, EmbeddingClientHandler>();
                services.AddTransient<IChatClientHandler, ChatClientHandler>();
                services.AddTransient<IOpenAIHandler, OpenAIHandler>();
                services.AddTransient<IChromaDbHandler, ChromaDbHandler>();
                services.AddTransient<ICustomSearchApiHandler, CustomSearchApiHandler>();
                services.AddTransient<IGoogleSearchHandler, GoogleSearchHandler>();
                services.AddTransient<IGistDebouncer, GistDebouncer>();

                services.AddHostedService(provider =>
                {
                    var options = provider.GetRequiredService<IOptionsSnapshot<MariaDbHandlerOptions>>()
                        .Get(gistMariaDbHandlerOptionsName);
                    return ActivatorUtilities.CreateInstance<GistService>(provider, options);
                });

                services.AddHostedService(provider =>
                {
                    var options = provider.GetRequiredService<IOptionsSnapshot<MariaDbHandlerOptions>>()
                        .Get(recapMariaDbHandlerOptionsName);
                    return ActivatorUtilities.CreateInstance<RecapService>(provider, options);
                });

                services.AddHostedService(provider =>
                {
                    var options = provider.GetRequiredService<IOptionsSnapshot<MariaDbHandlerOptions>>()
                        .Get(cleanupMariaDbHandlerOptionsName);
                    return ActivatorUtilities.CreateInstance<CleanupService>(provider, options);
                });
            })
            .ConfigureWebHostDefaults(webBuilder => {
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapMetrics();
                    });
                });
            })
            .Build();

        await host.RunAsync();
    }
}
