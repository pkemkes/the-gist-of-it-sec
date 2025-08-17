using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace GistBackend;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateDefaultBuilder(args);
        var host = builder.UseSerilog((_, _, configuration) => {
                configuration
                    .Enrich.FromLogContext()
                    .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
                    .MinimumLevel.Override("System.Net.Http.HttpClient.Default.LogicalHandler", LogEventLevel.Warning)
                    .MinimumLevel.Override("System.Net.Http.HttpClient.Default.ClientHandler", LogEventLevel.Warning)
                    .WriteTo.Console(new Serilog.Formatting.Json.JsonFormatter());
            })
            .ConfigureWebHostDefaults(webBuilder => webBuilder.UseStartup<StartUp>())
            .Build();

        await host.RunAsync();
    }
}
