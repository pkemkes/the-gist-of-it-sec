using GistBackend.Handler;
using GistBackend.Handler.ChromaDbHandler;
using GistBackend.Handler.GoogleSearchHandler;
using GistBackend.Handler.OpenAiHandler;
using GistBackend.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Prometheus;
using Serilog;

namespace GistBackend;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .UseSerilog((_, _, configuration) => {
                configuration
                    .Enrich.FromLogContext()
                    .WriteTo.Console(new Serilog.Formatting.Json.JsonFormatter());
            })
            .ConfigureServices((context, services) => {
                services.Configure<MariaDbHandlerOptions>(
                    context.Configuration.GetSection("MariaDbHandlerOptions"));
                services.Configure<EmbeddingClientHandlerOptions>(
                    context.Configuration.GetSection("EmbeddingClientHandlerOptions"));
                services.Configure<ChatClientHandlerOptions>(
                    context.Configuration.GetSection("ChatClientHandlerOptions"));
                services.Configure<ChromaDbHandlerOptions>(
                    context.Configuration.GetSection("ChromaDbHandlerOptions"));
                services.Configure<CustomSearchApiHandlerOptions>(
                    context.Configuration.GetSection("CustomSearchApiHandlerOptions"));

                services.AddHttpClient();
                services.AddTransient<IRssFeedHandler, RssFeedHandler>();
                services.AddTransient<IRssEntryHandler, RssEntryHandler>();
                services.AddTransient<IMariaDbHandler, MariaDbHandler>();
                services.AddTransient<IEmbeddingClientHandler, EmbeddingClientHandler>();
                services.AddTransient<IChatClientHandler, ChatClientHandler>();
                services.AddTransient<IOpenAIHandler, OpenAIHandler>();
                services.AddTransient<IChromaDbHandler, ChromaDbHandler>();
                services.AddTransient<ICustomSearchApiHandler, CustomSearchApiHandler>();
                services.AddTransient<IGoogleSearchHandler, GoogleSearchHandler>();
                services.AddHostedService<GistService>();
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
