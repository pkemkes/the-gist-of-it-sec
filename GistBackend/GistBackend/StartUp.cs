using GistBackend.Handlers;
using GistBackend.Handlers.ChromaDbHandler;
using GistBackend.Handlers.GoogleSearchHandler;
using GistBackend.Handlers.MariaDbHandler;
using GistBackend.Handlers.OpenAiHandler;
using GistBackend.Handlers.TelegramBotClientHandler;
using GistBackend.Services;
using GistBackend.Utils;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Prometheus;

namespace GistBackend;

public class StartUp(IConfiguration configuration)
{
    public const string RetryingHttpClientName = "WithRetry";
    public const string GistsControllerMariaDbHandlerOptionsName = $"GistsController{nameof(MariaDbHandlerOptions)}";

    public void ConfigureServices(IServiceCollection services)
    {
        const string gistMariaDbHandlerOptionsName = $"Gist{nameof(MariaDbHandlerOptions)}";
        const string recapMariaDbHandlerOptionsName = $"Recap{nameof(MariaDbHandlerOptions)}";
        const string cleanupMariaDbHandlerOptionsName = $"Cleanup{nameof(MariaDbHandlerOptions)}";
        const string telegramMariaDbHandlerOptionsName = $"Telegram{nameof(MariaDbHandlerOptions)}";
        const string dummyUserAgent = "Mozilla/5.0 (X11; Ubuntu; Linux x86_64; rv:131.0) Gecko/20100101 Firefox/131.0";

        services.Configure<EmbeddingClientHandlerOptions>(
            configuration.GetSection(nameof(EmbeddingClientHandlerOptions)));
        services.Configure<ChatClientHandlerOptions>(
            configuration.GetSection(nameof(ChatClientHandlerOptions)));
        services.Configure<ChromaDbHandlerOptions>(
            configuration.GetSection(nameof(ChromaDbHandlerOptions)));
        services.Configure<CustomSearchApiHandlerOptions>(
            configuration.GetSection(nameof(CustomSearchApiHandlerOptions)));
        services.Configure<TelegramBotClientHandlerOptions>(
            configuration.GetSection(nameof(TelegramBotClientHandlerOptions)));
        services.Configure<CleanupServiceOptions>(
            configuration.GetSection(nameof(CleanupServiceOptions)));
        services.Configure<TelegramServiceOptions>(
            configuration.GetSection(nameof(TelegramServiceOptions)));

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
        services.AddTransient<ITelegramBotClientHandler, TelegramBotClientHandler>();
        services.AddTransient<IDateTimeHandler, DateTimeHandler>();

        services.AddControllers();

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

        services.AddHostedService(provider =>
        {
            var options = provider.GetRequiredService<IOptionsSnapshot<TelegramServiceOptions>>()
                .Get(telegramMariaDbHandlerOptionsName);
            return ActivatorUtilities.CreateInstance<TelegramService>(provider, options);
        });

        services.AddKeyedScoped<IMariaDbHandler>(GistsControllerMariaDbHandlerOptionsName, (provider, _) => {
            var options = provider.GetRequiredService<IOptionsSnapshot<MariaDbHandlerOptions>>()
                .Get(GistsControllerMariaDbHandlerOptionsName);
            var dateTimeHandler = provider.GetRequiredService<IDateTimeHandler>();
            var logger = provider.GetService<ILogger<MariaDbHandler>>();
            return new MariaDbHandler(
                Options.Create(options),
                dateTimeHandler,
                logger
            );
        });
    }

    public static void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        app.UseRouting();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            endpoints.MapMetrics();
        });
    }
}
