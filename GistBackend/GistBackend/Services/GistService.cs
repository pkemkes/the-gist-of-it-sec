using GistBackend.Definitions;
using GistBackend.Handler;
using GistBackend.Types;
using GistBackend.Utils;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GistBackend.Services;

public class GistService(
    IMariaDbHandler mariaDbHandler,
    IOpenAIHandler openAIHandler,
    IChromaDbHandler chromaDbHandler,
    IGoogleSearchHandler googleSearchHandler,
    ILogger<GistService> logger
) : BackgroundService {
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var startTime = DateTimeOffset.UtcNow;
            await ProcessFeedsAsync(ct);
            await DelayUntilNextExecutionAsync(startTime, ct);
        }
    }

    private Task ProcessFeedsAsync(CancellationToken ct) => Task.WhenAll(
        DefinedFeeds.Definitions.Select(feed => ProcessFeedAsync(feed, ct))
    );

    private async Task ProcessFeedAsync(RssFeed feed, CancellationToken ct)
    {
        await feed.ParseFeedAsync(ct);


        foreach (var entry in feed.Entries.OrderBy(entry => entry.Updated)) await ProcessEntryAsync(entry, ct);
    }

    private async Task EnsureCurrentFeedInfoInDbAsync(RssFeed feed, CancellationToken ct)
    {
        var existingFeedInfo = await mariaDbHandler.GetFeedInfoByRssUrlAsync(feed.RssUrl, ct);
        if (existingFeedInfo is null)
        {
            // insert feed
            return;
        }

        var parsedFeedInfo = feed.ToRssFeedInfo() with { Id = existingFeedInfo.Id };
        if (parsedFeedInfo != existingFeedInfo)
        {
            // update feed
        }
    }

    private async Task ProcessEntryAsync(RssEntry entry, CancellationToken ct)
    {
        var existingGist = await mariaDbHandler.GetGistByReferenceAsync(entry.Reference, ct);
        // Skip if current version already exists in database
        if (existingGist is not null && existingGist.Updated == entry.Updated) return;

        var olderVersionExists = existingGist is not null;
        var aiResponse = await openAIHandler.ProcessEntryAsync(entry, ct);
        var gist = new Gist(entry, aiResponse);
        await UpsertGistAsync(gist, olderVersionExists, ct);
        await chromaDbHandler.UpsertEntryAsync(entry, ct);
        var searchResults = await googleSearchHandler.GetSearchResultsAsync(gist.SearchQuery, ct);

    }

    private Task UpsertGistAsync(Gist gist, bool olderVersionExists, CancellationToken ct) => olderVersionExists
        ? mariaDbHandler.UpdateGistAsync(gist, ct)
        : mariaDbHandler.InsertGistAsync(gist, ct);

    private async Task DelayUntilNextExecutionAsync(DateTimeOffset startTime, CancellationToken ct)
    {
        var delay = startTime.AddMinutes(5) - DateTimeOffset.UtcNow;
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, ct);
        }
        else
        {
            logger.LogWarning(LogEvents.GistServiceDelayExceeded,
                "Processing entries took longer than delay timeframe");
        }
    }
}
