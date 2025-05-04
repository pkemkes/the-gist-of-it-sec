using GistBackend.Handler;
using Microsoft.Extensions.Logging;

namespace GistBackend.Utils;

public static class ServiceUtils
{
    public static async Task DelayUntilNextExecutionAsync(DateTime startTime, ILogger? logger, CancellationToken ct,
        IDateTimeHandler? dateTimeHandler = null)
    {
        var now = dateTimeHandler?.GetUtcNow() ?? DateTime.UtcNow;
        var delay = startTime.AddMinutes(5) - now;
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, ct);
        }
        else
        {
            logger?.LogWarning(LogEvents.GistServiceDelayExceeded,
                "Processing entries took longer than delay timeframe");
        }
    }
}
