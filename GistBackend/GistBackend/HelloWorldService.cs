using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GistBackend;

public class HelloWorldService(ILogger<HelloWorldService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var nextRun = DateTimeOffset.UtcNow.AddSeconds(10);
            logger.LogInformation("Hello World!");
            
            var delay = nextRun - DateTimeOffset.UtcNow;
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, stoppingToken);
            }
        }
    }
}