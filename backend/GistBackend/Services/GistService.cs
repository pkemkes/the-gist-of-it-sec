using System.Diagnostics;
using GistBackend.Exceptions;
using GistBackend.Handlers;
using GistBackend.Handlers.ChromaDbHandler;
using GistBackend.Handlers.GoogleSearchHandler;
using GistBackend.Handlers.MariaDbHandler;
using GistBackend.Handlers.OpenAiHandler;
using GistBackend.Types;
using GistBackend.Utils;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using Prometheus;
using static GistBackend.Utils.LogEvents;
using static GistBackend.Utils.ServiceUtils;

namespace GistBackend.Services;

public class GistService(
    IRssFeedHandler rssFeedHandler,
    IWebCrawlHandler webCrawlHandler,
    IMariaDbHandler mariaDbHandler,
    IOpenAIHandler openAIHandler,
    IChromaDbHandler chromaDbHandler,
    IGoogleSearchHandler googleSearchHandler,
    ILogger<GistService>? logger = null
) : BackgroundService
{
    private static readonly Gauge ProcessFeedsGauge =
        Metrics.CreateGauge("process_feeds_seconds", "Time spent to process all feeds");
    private static readonly Summary ProcessEntrySummary =
        Metrics.CreateSummary("process_entry_seconds", "Time spent to process an entry", "feed_title");
    private static readonly Summary GetSearchResultsSummary =
        Metrics.CreateSummary("google_search_results_seconds", "Time spent to get search results");
    private static readonly Summary SummarizeEntrySummary =
        Metrics.CreateSummary("summarize_entry_seconds", "Time spent to summarize an entry");

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var startTime = DateTime.UtcNow;
            using (new SelfReportingStopwatch(elapsed => ProcessFeedsGauge.Set(elapsed)))
            {
                await ProcessFeedsAsync(ct);
            }
            await DelayUntilNextExecutionAsync(startTime, 5, logger, ct);
        }
    }

    private async Task ProcessFeedsAsync(CancellationToken ct)
    {
        foreach (var feed in rssFeedHandler.Definitions) await ProcessFeedAsync(feed, ct);
    }

    private async Task ProcessFeedAsync(RssFeed feed, CancellationToken ct)
    {
        await rssFeedHandler.ParseFeedAsync(feed, ct);
        var feedId = await EnsureCurrentFeedInfoInDbAsync(feed, ct);
        feed.ParseEntries(feedId);
        foreach (var entry in feed.Entries!.OrderBy(entry => entry.Updated)) await ProcessEntryAsync(entry, feed, ct);
    }

    private async Task<int> EnsureCurrentFeedInfoInDbAsync(RssFeed feed, CancellationToken ct)
    {
        var existingFeedInfo = await mariaDbHandler.GetFeedInfoByRssUrlAsync(feed.RssUrl, ct);
        var parsedFeedInfo = feed.ToRssFeedInfo();

        if (existingFeedInfo is null)
        {
            var feedId = await mariaDbHandler.InsertFeedInfoAsync(parsedFeedInfo, ct);
            return feedId;
        }
        if (parsedFeedInfo with { Id = existingFeedInfo.Id } != existingFeedInfo)
        {
            await mariaDbHandler.UpdateFeedInfoAsync(parsedFeedInfo, ct);
        }
        return existingFeedInfo.Id!.Value;
    }

    private async Task ProcessEntryAsync(RssEntry entry, RssFeed feed, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        using var loggingScope = logger?.BeginScope("Processing entry with reference {Reference}", entry.Reference);

        var existingGist = await mariaDbHandler.GetGistByReferenceAsync(entry.Reference, ct);
        var currentVersionAlreadyExists = existingGist is not null && existingGist.Updated == entry.Updated;
        if (currentVersionAlreadyExists)
        {
            await EnsureSearchResultInDbAsync(existingGist!, ct);
            return;
        }

        try
        {
            var pageText = await webCrawlHandler.FetchPageContentAsync(entry.Url.AbsoluteUri);

            var entryText = entry.ExtractText(pageText);
            var aiResponse = await GenerateAIResponse(entry.Title, entryText, ct);
            var gist = new Gist(entry, aiResponse);

            await chromaDbHandler.UpsertEntryAsync(entry, entryText, ct);

            if (existingGist is null) await InsertDataIntoDatabaseAsync(gist, ct);
            else await UpdateDataInDatabaseAsync(gist, existingGist.Id!.Value, ct);
            stopwatch.Stop();
            ProcessEntrySummary.WithLabels(feed.Title!).Observe(stopwatch.Elapsed.Seconds);
        }
        catch (ExtractingEntryTextException e)
        {
            logger?.LogWarning(ExtractingPageContentFailed, e, "Skipping entry, failed to extract text from page content for {Url}",
                entry.Url.AbsoluteUri);
        }
        catch (Exception e) when (e is PlaywrightException or TimeoutException)
        {
            logger?.LogWarning(FetchingPageContentFailed, e, "Skipping entry, failed to fetch page content for {Url}",
                entry.Url.AbsoluteUri);
        }
    }

    private async Task EnsureSearchResultInDbAsync(Gist gist, CancellationToken ct)
    {
        var searchResults = await mariaDbHandler.GetSearchResultsByGistIdAsync(gist.Id!.Value, ct);
        if (searchResults.Count != 0) return;
        await FetchAndInsertSearchResultAsync(gist.SearchQuery, gist.Id!.Value, ct);
    }

    private async Task<SummaryAIResponse> GenerateAIResponse(string entryTitle, string text, CancellationToken ct)
    {
        using (new SelfReportingStopwatch(elapsed => SummarizeEntrySummary.Observe(elapsed)))
        {
            return await openAIHandler.GenerateSummaryTagsAndQueryAsync(entryTitle, text, ct);
        }
    }

    private async Task InsertDataIntoDatabaseAsync(Gist gist, CancellationToken ct)
    {
        var gistId = await mariaDbHandler.InsertGistAsync(gist, ct);
        using var loggingScope = logger?.BeginScope("Processing gist with ID {GistId}", gistId);
        logger?.LogInformation(GistInserted, "Gist inserted");

        await FetchAndInsertSearchResultAsync(gist.SearchQuery, gistId, ct);
    }

    private async Task FetchAndInsertSearchResultAsync(string searchQuery, int gistId, CancellationToken ct)
    {
        var searchResults = await GetSearchResultsAsync(searchQuery, gistId, ct);
        if (searchResults is null) return;
        await mariaDbHandler.InsertSearchResultsAsync(searchResults, ct);
        logger?.LogInformation(SearchResultsInserted, "Search Results inserted for gist with ID {GistId}", gistId);
    }

    private async Task UpdateDataInDatabaseAsync(Gist gist, int gistId, CancellationToken ct)
    {
        await mariaDbHandler.UpdateGistAsync(gist, ct);
        logger?.LogInformation(GistUpdated, "Gist with referenced {Reference} updated at ID {Id}",
            gist.Reference, gistId);

        var searchResults = await GetSearchResultsAsync(gist.SearchQuery, gistId, ct);
        if (searchResults is null) return;
        await mariaDbHandler.UpdateSearchResultsAsync(searchResults, ct);
        logger?.LogInformation(SearchResultsUpdated, "Search Results updated for gist with ID {GistId}", gistId);
    }

    private async Task<List<GoogleSearchResult>?> GetSearchResultsAsync(string searchQuery, int gistId, CancellationToken ct)
    {
        using (new SelfReportingStopwatch(elapsed => GetSearchResultsSummary.Observe(elapsed)))
        {
            var searchResults = await googleSearchHandler.GetSearchResultsAsync(searchQuery, gistId, ct);
            if (searchResults is not null && searchResults.Count != 0) return searchResults;
        }

        logger?.LogWarning(NoSearchResults, "No search results found for gist");
        return null;
    }
}
