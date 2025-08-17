using GistBackend.Handlers;
using Microsoft.Extensions.Logging;
using static GistBackend.Utils.LogEvents;

namespace GistBackend.Utils;

public static class ServiceUtils
{
    public static async Task DelayUntilNextExecutionAsync(DateTime startTime, double delayMinutes, ILogger? logger,
        CancellationToken ct, IDateTimeHandler? dateTimeHandler = null)
    {
        var now = dateTimeHandler?.GetUtcNow() ?? DateTime.UtcNow;
        var delay = startTime.AddMinutes(delayMinutes) - now;
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, ct);
        }
        else
        {
            logger?.LogWarning(GistServiceDelayExceeded,
                "Processing entries took longer than delay timeframe");
        }
    }
}
