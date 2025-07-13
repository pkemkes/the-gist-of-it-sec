using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace GistBackend;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateDefaultBuilder(args);
        var host = builder.UseSerilog((_, _, configuration) => {
                configuration
                    .Enrich.FromLogContext()
                    .WriteTo.Console(new Serilog.Formatting.Json.JsonFormatter());
            })
            .ConfigureWebHostDefaults(webBuilder => webBuilder.UseStartup<StartUp>())
            .Build();

        await host.RunAsync();
    }
}
