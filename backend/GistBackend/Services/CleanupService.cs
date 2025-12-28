using GistBackend.Exceptions;
using GistBackend.Handlers;
using GistBackend.Handlers.ChromaDbHandler;
using GistBackend.Handlers.MariaDbHandler;
using GistBackend.Types;
using GistBackend.Utils;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using Prometheus;
using static GistBackend.Utils.LogEvents;
using Summary = Prometheus.Summary;

namespace GistBackend.Services;

public class CleanupService(
    IRssFeedHandler rssFeedHandler,
    IGistDebouncer gistDebouncer,
    IMariaDbHandler mariaDbHandler,
    IChromaDbHandler chromaDbHandler,
    IWebCrawlHandler webCrawlHandler,
    IOptions<CleanupServiceOptions> options,
    ILogger<CleanupService>? logger)
    : BackgroundService
{
    private static readonly Gauge CleanupGistsGauge =
        Metrics.CreateGauge("cleanup_gists_seconds", "Time spent to cleanup gists");
    private static readonly Summary CheckGistSummary =
        Metrics.CreateSummary("check_gist_seconds", "Time spent to check a gist", "feed_title");
    private static readonly Gauge GistsCheckedGauge =
        Metrics.CreateGauge("gists_checked", "Number of gists checked in one run");
    private List<int> _feedsInDb = [];
    private Dictionary<int, RssFeed> _feedsByFeedId = new();

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var startTime = DateTime.UtcNow;
            _feedsInDb = [];
            _feedsByFeedId = new Dictionary<int, RssFeed>();
            using (new SelfReportingStopwatch(elapsed => CleanupGistsGauge.Set(elapsed)))
            {
                await ParseFeedsAsync(ct);
                await CleanupGistsAsync(ct);
            }
            await ServiceUtils.DelayUntilNextExecutionAsync(startTime, 15, logger, ct);
        }
    }

    private async Task ParseFeedsAsync(CancellationToken ct)
    {
        foreach (var feed in rssFeedHandler.Definitions) await ParseAndCacheFeedAsync(feed, ct);
    }

    private async Task ParseAndCacheFeedAsync(RssFeed feed, CancellationToken ct)
    {
        using var _ = logger?.BeginScope(new Dictionary<string, object> { ["RssUrl"] = feed.RssUrl });
        try
        {
            await rssFeedHandler.ParseFeedAsync(feed, ct);
            var feedInfo = await mariaDbHandler.GetFeedInfoByRssUrlAsync(feed.RssUrl, ct);
            if (feedInfo is null)
            {
                logger?.LogWarning(DidNotFindExpectedFeedInDb, "Could not find feed in db: {RssUrl}", feed.RssUrl);
            }
            else
            {
                _feedsInDb.Add(feedInfo.Id!.Value);
                feed.ParseEntries(feedInfo.Id!.Value);
                _feedsByFeedId.Add(feedInfo.Id!.Value, feed);
            }
        }
        catch (ParsingFeedException e)
        {
            logger?.LogWarning(ParsingFeedFailed, e, "Skipping feed, failed to parse RSS feed from {RssUrl}",
                feed.RssUrl);
        }
    }

    private async Task CleanupGistsAsync(CancellationToken ct)
    {
        var allGists = await mariaDbHandler.GetAllGistsAsync(ct);
        var readyGists = allGists.Where(gist => gistDebouncer.IsReady(gist.Id!.Value, gist.Updated)).ToList();
        foreach (var gist in readyGists) await CheckGistAsync(gist, ct);
        GistsCheckedGauge.Set(readyGists.Count);
    }

    private async Task CheckGistAsync(Gist gist, CancellationToken ct)
    {
        try
        {
            if (!_feedsByFeedId.ContainsKey(gist.FeedId))
            {
                if (!_feedsInDb.Contains(gist.FeedId))
                {
                    throw new FeedNotFoundException($"Feed with ID {gist.FeedId} not found");
                }
                return;
            }
            var shouldBeDisabled = await GistShouldBeDisabledAsync(gist);
            await mariaDbHandler.EnsureCorrectDisabledStateForGistAsync(gist.Id!.Value, shouldBeDisabled, ct);
            await chromaDbHandler.EnsureGistHasCorrectMetadataAsync(gist, shouldBeDisabled, ct);
        }
        catch (Exception e) when (e is PlaywrightException or TimeoutException)
        {
            logger?.LogError(FetchingPageContentFailed, e, "Skipping gist, failed to fetch page content for {Url}",
                gist.Url.AbsoluteUri);
        }
    }

    private async Task<bool> GistShouldBeDisabledAsync(Gist gist)
    {
        if (options.Value.DomainsToIgnore.Any(domain => gist.Url.Host.Equals(domain))) return false;
        var feedTitle = GetFeedTitleByFeedId(gist.FeedId);
        using (new SelfReportingStopwatch(elapsed => CheckGistSummary.WithLabels(feedTitle).Observe(elapsed)))
        {
            var response = await webCrawlHandler.FetchResponseAsync(gist.Url.AbsoluteUri);

            if (response is null) return true;
            if (response.Status is >= 400 and < 500) return true;
            return WasRedirectedAndNotPresentInFeedAnymore(gist, response);
        }
    }

    private string GetFeedTitleByFeedId(int feedId)
    {
        if (!_feedsByFeedId.TryGetValue(feedId, out var feed))
        {
            throw new FeedNotFoundException($"Feed with ID {feedId} not found");
        }
        return feed.Title ?? throw new InvalidOperationException($"Feed with ID {feedId} has no title");
    }

    private bool WasRedirectedAndNotPresentInFeedAnymore(Gist gist, IResponse response)
    {
        var wasRedirected = gist.Url.AbsoluteUri != response.Request.Url;
        var isPresentInFeed = _feedsByFeedId[gist.FeedId].Entries!.Any(entry => entry.Url == gist.Url);
        return wasRedirected && !isPresentInFeed;
    }
}
