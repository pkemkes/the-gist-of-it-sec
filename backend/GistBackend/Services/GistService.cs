using System.Diagnostics;
using GistBackend.Exceptions;
using GistBackend.Handlers.AIHandler;
using GistBackend.Handlers.ChromaDbHandler;
using GistBackend.Handlers.MariaDbHandler;
using GistBackend.Handlers.RssFeedHandler;
using GistBackend.Handlers.WebCrawlHandler;
using GistBackend.Types;
using GistBackend.Utils;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Prometheus;
using static GistBackend.Utils.LogEvents;
using static GistBackend.Utils.ServiceUtils;
using PrometheusSummary = Prometheus.Summary;
using Summary = GistBackend.Types.Summary;

namespace GistBackend.Services;

public class GistService(
    IRssFeedHandler rssFeedHandler,
    IWebCrawlHandler webCrawlHandler,
    IMariaDbHandler mariaDbHandler,
    IAIHandler aiHandler,
    IChromaDbHandler chromaDbHandler,
    ILogger<GistService>? logger = null
) : BackgroundService
{
    private static readonly Gauge ProcessFeedsGauge =
        Metrics.CreateGauge("process_feeds_seconds", "Time spent to process all feeds");
    private static readonly PrometheusSummary ProcessEntrySummary =
        Metrics.CreateSummary("process_entry_seconds", "Time spent to process an entry", "feed_title");
    private static readonly PrometheusSummary SummarizeEntrySummary =
        Metrics.CreateSummary("summarize_entry_seconds", "Time spent to summarize an entry");

    private readonly Dictionary<int, RssFeed> _feedsByFeedId = new();

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var startTime = DateTime.UtcNow;
            using (new SelfReportingStopwatch(elapsed => ProcessFeedsGauge.Set(elapsed)))
            {
                await ProcessFeedsAsync(ct);
                await Task.WhenAll(_feedsByFeedId.Values.Select(feed => ProcessEntriesAsync(feed.Entries!.OrderBy(e => e.Updated), ct)));
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
        try
        {
            await rssFeedHandler.ParseFeedAsync(feed, ct);
            var feedId = await EnsureCurrentFeedInfoInDbAsync(feed, ct);
            feed.ParseEntries(feedId);
            _feedsByFeedId[feedId] = feed;
        }
        catch (ParsingFeedException e)
        {
            logger?.LogWarning(ParsingFeedFailed, e, "Skipping feed, failed to parse RSS feed from {RssUrl}",
                feed.RssUrl);
        }
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

    private async Task ProcessEntriesAsync(IEnumerable<RssEntry> entries, CancellationToken ct)
    {
        foreach (var entry in entries) await ProcessEntryAsync(entry, ct);
    }

    private async Task ProcessEntryAsync(RssEntry entry, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        using var loggingScope = logger?.BeginScope("Processing entry with reference {Reference}", entry.Reference);

        var existingGist = await mariaDbHandler.GetGistByReferenceAsync(entry.Reference, ct);
        var currentVersionAlreadyExists = existingGist is not null && existingGist.Updated == entry.Updated;
        if (currentVersionAlreadyExists) return;

        try
        {
            var feed = _feedsByFeedId[entry.FeedId];
            var fetchResponse = await webCrawlHandler.FetchAsync(entry.Url.AbsoluteUri, ct);
            if (fetchResponse.Status != 200)
            {
                logger?.LogWarning(FetchingPageContentFailed,
                    "Skipping entry, fetched page content returned status {Status} for {Url}", fetchResponse.Status,
                    entry.Url.AbsoluteUri);
                return;
            }

            var entryText = feed.ExtractText(fetchResponse.Content);
            var summaryAIResponse = await GenerateSummaryAIResponse(feed.Language, entry.Title, entryText, ct);
            var isSponsoredContent = feed.CheckForSponsoredContent(fetchResponse.Content);

            var gist = new Gist(entry, summaryAIResponse, isSponsoredContent);

            await chromaDbHandler.UpsertEntryAsync(entry, summaryAIResponse.SummaryEnglish, ct);

            if (existingGist is null)
            {
                await InsertDataIntoDatabaseAsync(entry, gist, summaryAIResponse, feed.Language, ct);
            }
            else
            {
                await UpdateDataInDatabaseAsync(entry, existingGist.Id!.Value, gist, summaryAIResponse, feed.Language,
                    ct);
            }

            stopwatch.Stop();
            ProcessEntrySummary.WithLabels(feed.Title!).Observe(stopwatch.Elapsed.Seconds);
        }
        catch (ExtractingEntryTextException e)
        {
            logger?.LogWarning(ExtractingPageContentFailed, e, "Skipping entry, failed to extract text from page content for {Url}",
                entry.Url.AbsoluteUri);
        }
        catch (Exception e) when (e is ExternalServiceException or HttpRequestException)
        {
            logger?.LogWarning(FetchingPageContentFailed, e, "Skipping entry, failed to fetch page content for {Url}",
                entry.Url.AbsoluteUri);
        }
    }

    private async Task<SummaryAIResponse> GenerateSummaryAIResponse(Language feedLanguage, string entryTitle,
        string text, CancellationToken ct)
    {
        using var stopwatch = new SelfReportingStopwatch(elapsed => SummarizeEntrySummary.Observe(elapsed));
        return await aiHandler.GenerateSummaryAIResponseAsync(feedLanguage, entryTitle, text, ct);
    }

    private async Task InsertDataIntoDatabaseAsync(RssEntry entry, Gist gist, SummaryAIResponse summaryAIResponse,
        Language feedLanguage, CancellationToken ct)
    {
        await using var handle = await mariaDbHandler.OpenTransactionAsync(ct);
        try
        {
            var gistId = await mariaDbHandler.InsertGistAsync(gist, handle, ct);

            var germanSummary = CreateSummary(gistId, entry, summaryAIResponse, feedLanguage, Language.De);
            await mariaDbHandler.InsertSummaryAsync(germanSummary, handle, ct);

            var englishSummary = CreateSummary(gistId, entry, summaryAIResponse, feedLanguage, Language.En);
            await mariaDbHandler.InsertSummaryAsync(englishSummary, handle, ct);

            await handle.Transaction.CommitAsync(ct);
            logger?.LogInformation(GistInserted, "Gist inserted with ID {Id}", gistId);
        }
        catch (Exception e)
        {
            logger?.LogError(InsertingGistFailed, e, "Failed to insert gist with Reference {Reference}",
                gist.Reference);
            await handle.Transaction.RollbackAsync(ct);
            throw;
        }
    }

    private async Task UpdateDataInDatabaseAsync(RssEntry entry, int gistId, Gist gist,
        SummaryAIResponse summaryAIResponse, Language feedLanguage, CancellationToken ct)
    {
        await using var handle = await mariaDbHandler.OpenTransactionAsync(ct);
        try
        {
            await mariaDbHandler.UpdateGistAsync(gist, handle, ct);

            var germanSummary = CreateSummary(gistId, entry, summaryAIResponse, feedLanguage, Language.De);
            await mariaDbHandler.UpdateSummaryAsync(germanSummary, handle, ct);

            var englishSummary = CreateSummary(gistId, entry, summaryAIResponse, feedLanguage, Language.En);
            await mariaDbHandler.UpdateSummaryAsync(englishSummary, handle, ct);

            await handle.Transaction.CommitAsync(ct);
            logger?.LogInformation(GistUpdated, "Gist updated at ID {Id}", gistId);
        }
        catch (Exception e)
        {
            logger?.LogError(UpdatingGistFailed, e, "Failed to update gist with ID {Id}", gistId);
            await handle.Transaction.RollbackAsync(ct);
            throw;
        }
    }

    private static Summary CreateSummary(int gistId, RssEntry entry, SummaryAIResponse summaryAIResponse,
        Language feedLanguage, Language summaryLanguage) =>
        new(gistId, summaryLanguage, feedLanguage != summaryLanguage,
            feedLanguage == summaryLanguage ? entry.Title : summaryAIResponse.TitleTranslated,
            summaryLanguage == Language.En ? summaryAIResponse.SummaryEnglish : summaryAIResponse.SummaryGerman);
}
