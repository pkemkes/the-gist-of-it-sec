using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GistBackend;

public class GistService(ILogger<GistService> logger) : BackgroundService {
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var nextRun = DateTimeOffset.UtcNow.AddMinutes(5);
            await Task.WhenAll(DefinedFeeds.Definitions.Select(feed => feed.ParseFeedAsync(ct)));
            await Task.WhenAll(
                DefinedFeeds.Definitions
                    .SelectMany(feed => feed.Entries)
                    .OrderBy(entry => entry.Updated).ToList()
                    .Select(entry => ProcessEntryAsync(entry, ct))
            );
        }
    }

    private Task ProcessFeedsAsync(CancellationToken ct) => Task.WhenAll(
        DefinedFeeds.Definitions.Select(feed => ProcessEntriesAsync(feed.Entries.OrderBy(entry => entry.Updated), ct))
    );

    private async Task ProcessEntriesAsync(IEnumerable<RssEntry> entries, CancellationToken ct)
    {
        foreach (var entry in entries)
        {
            await ProcessEntryAsync(entry, ct);
        }
    }

    private Task ProcessEntryAsync(RssEntry entry, CancellationToken ct)
    {
        return Task.Delay(100, ct);
    }
}
