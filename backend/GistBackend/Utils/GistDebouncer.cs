using GistBackend.Handlers;

namespace GistBackend.Utils;

public interface IGistDebouncer
{
    bool IsReady(int gistId, DateTime updated);
}

public class GistDebouncer(IDateTimeHandler dateTimeHandler) : IGistDebouncer
{
    private readonly Dictionary<int, DateTime> _readyTimesByGistId = new();
    private static readonly Random Random = new();

    public bool IsReady(int gistId, DateTime updated)
    {
        if (_readyTimesByGistId.TryGetValue(gistId, out var readyTime))
        {
            if (readyTime > dateTimeHandler.GetUtcNow()) return false;
            DebounceGist(gistId, updated);
            return true;
        }
        DebounceGist(gistId, updated);
        return false;
    }

    private void DebounceGist(int gistId, DateTime updated)
    {
        _readyTimesByGistId[gistId] = CalculateReadyTime(updated);
    }

    private DateTime CalculateReadyTime(DateTime updated)
    {
        var now = dateTimeHandler.GetUtcNow();
        var age = now - updated;

        TimeSpan meanDebounceDuration;
        if (age < TimeSpan.FromHours(1)) meanDebounceDuration = TimeSpan.FromMinutes(30);
        else if (age < TimeSpan.FromHours(6)) meanDebounceDuration = TimeSpan.FromHours(1);
        else if (age < TimeSpan.FromDays(1)) meanDebounceDuration = TimeSpan.FromHours(3);
        else if (age < TimeSpan.FromDays(7)) meanDebounceDuration = TimeSpan.FromHours(6);
        else meanDebounceDuration = TimeSpan.FromDays(1);

        // Generate random jitter between -meanDebounceDuration/2 and +meanDebounceDuration/2
        var halfDuration = meanDebounceDuration / 2;
        var randomFactor = Random.NextDouble() * 2 - 1; // [-1, 1]
        var jitter = halfDuration * randomFactor;

        return now + meanDebounceDuration + jitter;
    }
}
