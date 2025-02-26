using GistBackend.Definitions;
using GistBackend.Handler;
using GistBackend.Types;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GistBackend.Services;

public class GistService(
    IMariaDbHandler mariaDbHandler,
    IOpenAIHandler openAIHandler,
    ILogger<GistService> logger
) : BackgroundService {
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
        foreach (var entry in entries) await ProcessEntryAsync(entry, ct);
    }

    private async Task ProcessEntryAsync(RssEntry entry, CancellationToken ct)
    {
        var updated = await mariaDbHandler.GetGistUpdatedByReferenceIfExistsAsync(entry.Reference, ct);
        if (updated == entry.Updated) return; // Current version already exists in database
        var olderVersionExists = updated is not null;
        var aiResponse = await openAIHandler.ProcessEntryAsync(entry, ct);
        var gist = new Gist(entry, aiResponse);
        await mariaDbHandler.UpsertGistAsync(gist, olderVersionExists, ct);
    }
}
