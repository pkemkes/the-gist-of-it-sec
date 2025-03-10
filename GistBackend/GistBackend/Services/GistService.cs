using GistBackend.Definitions;
using GistBackend.Handler;
using GistBackend.Handler.ChromaDbHandler;
using GistBackend.Handler.OpenAiHandler;
using GistBackend.Types;
using GistBackend.Utils;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GistBackend.Services;

public class GistService(
    IRssEntryHandler rssEntryHandler,
    IMariaDbHandler mariaDbHandler,
    IOpenAIHandler openAIHandler,
    IChromaDbHandler chromaDbHandler,
    IGoogleSearchHandler googleSearchHandler,
    ILogger<GistService>? logger = null
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
        await EnsureCurrentFeedInfoInDbAsync(feed, ct);
        foreach (var entry in feed.Entries.OrderBy(entry => entry.Updated)) await ProcessEntryAsync(entry, ct);
    }

    private async Task EnsureCurrentFeedInfoInDbAsync(RssFeed feed, CancellationToken ct)
    {
        var existingFeedInfo = await mariaDbHandler.GetFeedInfoByRssUrlAsync(feed.RssUrl, ct);
        var parsedFeedInfo = feed.ToRssFeedInfo();

        if (existingFeedInfo is null) await mariaDbHandler.InsertFeedInfoAsync(parsedFeedInfo, ct);
        else if (parsedFeedInfo with { Id = existingFeedInfo.Id } != existingFeedInfo)
        {
            await mariaDbHandler.UpdateFeedInfoAsync(parsedFeedInfo, ct);
        }
    }

    private async Task ProcessEntryAsync(RssEntry entry, CancellationToken ct)
    {
        var existingGist = await mariaDbHandler.GetGistByReferenceAsync(entry.Reference, ct);
        var currentVersionAlreadyExists = existingGist is not null && existingGist.Updated == entry.Updated;
        if (currentVersionAlreadyExists) return;

        var text = await rssEntryHandler.FetchTextContentAsync(entry, ct);
        var aiResponse = await openAIHandler.GenerateSummaryTagsAndQueryAsync(entry.Title, text, ct);
        var gist = new Gist(entry, aiResponse);

        await chromaDbHandler.InsertEntryAsync(entry, text, ct);
        logger?.LogInformation(LogEvents.DocumentInserted,
            "Documented with reference {Reference} inserted into ChromaDB", entry.Reference);

        if (existingGist is null) await InsertDataIntoDatabaseAsync(gist, ct);
        else await UpdateDataInDatabaseAsync(gist, existingGist.Id!.Value, ct);
    }

    private async Task InsertDataIntoDatabaseAsync(Gist gist, CancellationToken ct)
    {
        var gistId = await mariaDbHandler.InsertGistAsync(gist, ct);
        logger?.LogInformation(LogEvents.GistInserted, "Gist with referenced {Reference} inserted at ID {Id}",
            gist.Reference, gistId);

        var searchResults = await googleSearchHandler.GetSearchResultsAsync(gist.SearchQuery, gistId, ct);
        await mariaDbHandler.InsertSearchResultsAsync(searchResults, ct);
        logger?.LogInformation(LogEvents.SearchResultsInserted, "Search Results inserted for gist with ID {GistId}",
            gistId);
    }

    private async Task UpdateDataInDatabaseAsync(Gist gist, int gistId, CancellationToken ct)
    {
        await mariaDbHandler.UpdateGistAsync(gist, ct);
        logger?.LogInformation(LogEvents.GistUpdated, "Gist with referenced {Reference} updated at ID {Id}",
            gist.Reference, gistId);

        var searchResults = await googleSearchHandler.GetSearchResultsAsync(gist.SearchQuery, gistId, ct);
        await mariaDbHandler.UpdateSearchResultsAsync(searchResults, ct);
        logger?.LogInformation(LogEvents.SearchResultsUpdated, "Search Results updated for gist with ID {GistId}",
            gistId);
    }

    private async Task DelayUntilNextExecutionAsync(DateTimeOffset startTime, CancellationToken ct)
    {
        var delay = startTime.AddMinutes(5) - DateTimeOffset.UtcNow;
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
