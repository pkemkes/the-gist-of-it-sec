using GistBackend.Handlers;
using GistBackend.Handlers.ChromaDbHandler;
using GistBackend.Handlers.GoogleSearchHandler;
using GistBackend.Handlers.MariaDbHandler;
using GistBackend.Handlers.OpenAiHandler;
using GistBackend.Services;
using GistBackend.Types;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TestUtilities;
using static TestUtilities.TestData;

// ReSharper disable AsyncVoidLambda

namespace GistBackend.UnitTest;

public class GistServiceTests
{
    [Fact]
    public async Task StartAsync_FeedInfosDoNotExistInDb_FeedInfosAreInserted()
    {
        var testFeeds = CreateTestFeeds();
        var mariaDbHandlerMock = Substitute.For<IMariaDbHandler>();
        mariaDbHandlerMock
            .GetFeedInfoByRssUrlAsync(Arg.Any<Uri>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(null as RssFeedInfo));
        var gistService = CreateGistService(mariaDbHandlerMock: mariaDbHandlerMock, testFeeds: testFeeds);

        await gistService.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        foreach (var testFeed in testFeeds)
        {
            await mariaDbHandlerMock
                .Received(1)
                .InsertFeedInfoAsync(testFeed.RssFeedInfo with { Id = null }, Arg.Any<CancellationToken>());
        }
    }

    [Fact]
    public async Task StartAsync_DifferentFeedInfoExistsInDb_FeedInfoIsUpdated()
    {
        var oldFeed = new TestFeedData {
            RssFeed = {
                Id = 0
            }
        };
        var newFeed = new TestFeedData {
            RssFeed = {
                Id = oldFeed.RssFeed.Id
            }
        };
        var mariaDbHandlerMock = Substitute.For<IMariaDbHandler>();
        mariaDbHandlerMock
            .GetFeedInfoByRssUrlAsync(newFeed.RssFeed.RssUrl, Arg.Any<CancellationToken>())!
            .Returns(Task.FromResult(oldFeed.RssFeedInfo));
        var gistService = CreateGistService(
            testFeeds: [ oldFeed, newFeed ],
            mariaDbHandlerMock: mariaDbHandlerMock
        );

        await gistService.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        await mariaDbHandlerMock.Received(1).UpdateFeedInfoAsync(newFeed.RssFeedInfo, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_TwoTestEntriesInFeed_EntriesProcessedFromOldestToNewest()
    {
        var testFeed = new TestFeedData(CreateTestEntries(5).OrderByDescending(entry => entry.Updated).ToList());
        var mariaDbHandlerMock = Substitute.For<IMariaDbHandler>();
        var gistService = CreateGistService(mariaDbHandlerMock: mariaDbHandlerMock, testFeeds: [ testFeed ]);

        await gistService.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        var orderedEntries = testFeed.Entries.OrderBy(entry => entry.Updated).ToArray();
        Received.InOrder(async () => {
            foreach (var entry in orderedEntries)
                await mariaDbHandlerMock.GetGistByReferenceAsync(entry.Reference, Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task StartAsync_EntryAlreadyExistInDbButSearchResultsDoNotForSomeGists_SearchResultsAreRequestedAndInserted()
    {
        var testFeed = new TestFeedData();
        var gistsWithSearchResults = testFeed.Gists.Skip(2).ToList();
        var gistsWithoutSearchResults = testFeed.Gists.Take(2).ToList();
        var searchResults = testFeed.Gists.Select(gist => CreateTestSearchResults(10, gist.Id!.Value)).ToList();
        var existingSearchResults = searchResults.Skip(2).ToList();
        var missingSearchResults = searchResults.Take(2).ToList();
        var mariaDbHandlerMock = Substitute.For<IMariaDbHandler>();
        testFeed.Gists.ForEach(gist =>
            mariaDbHandlerMock.GetGistByReferenceAsync(gist.Reference, Arg.Any<CancellationToken>())!.Returns(
                Task.FromResult(gist))
        );
        foreach (var (gist, searchResult) in gistsWithSearchResults.Zip(existingSearchResults))
        {
            mariaDbHandlerMock
                .GetSearchResultsByGistIdAsync(gist.Id!.Value, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(searchResult));
        }
        var googleSearchHandlerMock = Substitute.For<IGoogleSearchHandler>();
        foreach (var (gist, searchResult) in gistsWithoutSearchResults.Zip(missingSearchResults))
        {
            mariaDbHandlerMock
                .GetSearchResultsByGistIdAsync(gist.Id!.Value, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(new List<GoogleSearchResult>()));
            googleSearchHandlerMock
                .GetSearchResultsAsync(gist.SearchQuery, gist.Id!.Value,
                    Arg.Any<CancellationToken>())!.Returns(Task.FromResult(searchResult));
        }
        var gistService = CreateGistService(
            testFeeds: [testFeed],
            mariaDbHandlerMock: mariaDbHandlerMock,
            googleSearchHandlerMock: googleSearchHandlerMock
        );

        await gistService.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        foreach (var searchResult in missingSearchResults)
            await mariaDbHandlerMock.Received(1).InsertSearchResultsAsync(searchResult, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_EntryAndSearchResultAlreadyExistInDb_SearchResultIsNeitherRequestedNorInserted()
    {
        var testFeed = new TestFeedData();
        var testSearchResults = testFeed.Gists.Select(gist => CreateTestSearchResults(10, gist.Id!.Value)).ToList();
        var mariaDbHandlerMock = Substitute.For<IMariaDbHandler>();
        testFeed.Gists.ForEach(gist =>
            mariaDbHandlerMock.GetGistByReferenceAsync(gist.Reference, Arg.Any<CancellationToken>())!
                .Returns(Task.FromResult(gist))
        );
        foreach (var (gist, searchResults) in testFeed.Gists.Zip(testSearchResults))
        {
            mariaDbHandlerMock
                .GetSearchResultsByGistIdAsync(gist.Id!.Value, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(searchResults));
        }
        var googleSearchHandlerMock = Substitute.For<IGoogleSearchHandler>();
        var gistService = CreateGistService(
            testFeeds: [testFeed],
            mariaDbHandlerMock: mariaDbHandlerMock,
            googleSearchHandlerMock: googleSearchHandlerMock
        );

        await gistService.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        await googleSearchHandlerMock.DidNotReceive()
            .GetSearchResultsAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        await mariaDbHandlerMock.DidNotReceive()
            .InsertSearchResultsAsync(Arg.Any<IEnumerable<GoogleSearchResult>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_OldVersionOfGistsExist_GistsAreGeneratedAndUpdated()
    {
        var testFeed = new TestFeedData(feedId: 0);
        var testSearchResults = testFeed.Gists.Select(gist => CreateTestSearchResults(10, gist.Id!.Value)).ToList();
        var mariaDbHandlerMock = Substitute.For<IMariaDbHandler>();
        testFeed.Gists.ForEach(gist =>
            mariaDbHandlerMock.GetGistByReferenceAsync(gist.Reference, Arg.Any<CancellationToken>())!
                .Returns(Task.FromResult(gist with { Updated = gist.Updated.AddDays(-5)}))
        );
        var chromaDbHandlerMock = Substitute.For<IChromaDbHandler>();
        var googleSearchHandlerMock = Substitute.For<IGoogleSearchHandler>();
        foreach (var (gist, searchResults) in testFeed.Gists.Zip(testSearchResults))
        {
            googleSearchHandlerMock.GetSearchResultsAsync(gist.SearchQuery, gist.Id!.Value,
                Arg.Any<CancellationToken>())!.Returns(Task.FromResult(searchResults));
        }
        var gistService = CreateGistService(
            testFeeds: [testFeed],
            mariaDbHandlerMock: mariaDbHandlerMock,
            chromaDbHandlerMock: chromaDbHandlerMock,
            googleSearchHandlerMock: googleSearchHandlerMock
        );

        await gistService.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        await Task.WhenAll(testFeed.Entries.Select(entry =>
            chromaDbHandlerMock.Received(1).InsertEntryAsync(
                Arg.Is<RssEntry>(e => e.Reference == entry.Reference && e.FeedId == entry.FeedId),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())));
        await Task.WhenAll(testFeed.Entries.Select(entry =>
            mariaDbHandlerMock.Received(1)
                .UpdateGistAsync(
                    Arg.Is<Gist>(gist => gist.Reference == entry.Reference && gist.Updated == entry.Updated),
                    Arg.Any<CancellationToken>())));
        await Task.WhenAll(testSearchResults.Select(searchResults =>
            mariaDbHandlerMock.UpdateSearchResultsAsync(searchResults, Arg.Any<CancellationToken>())));
        await mariaDbHandlerMock.DidNotReceive().InsertGistAsync(Arg.Any<Gist>(), Arg.Any<CancellationToken>());
        await mariaDbHandlerMock.DidNotReceive()
            .InsertSearchResultsAsync(Arg.Any<IEnumerable<GoogleSearchResult>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_GistDoesNotExist_GistIsGeneratedAndInserted()
    {
        var testFeed = new TestFeedData(feedId: 0);
        var testSearchResults = testFeed.Entries.Select(_ => CreateTestSearchResults(10)).ToList();
        var testTexts = CreateTestStrings(testFeed.Entries.Count);
        var mariaDbHandlerMock = Substitute.For<IMariaDbHandler>();
        var chromaDbHandlerMock = Substitute.For<IChromaDbHandler>();
        var gistService = CreateGistService(
            mariaDbHandlerMock: mariaDbHandlerMock,
            chromaDbHandlerMock: chromaDbHandlerMock,
            testSearchResults: testSearchResults,
            testTexts: testTexts,
            testFeeds: [testFeed]
        );

        await gistService.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        foreach (var (entry, text) in testFeed.Entries.Zip(testTexts))
        {
            await chromaDbHandlerMock.Received(1)
                .InsertEntryAsync(Arg.Is<RssEntry>(e => e.Reference == entry.Reference && e.FeedId == entry.FeedId),
                    text,
                    Arg.Any<CancellationToken>());
        }
        await Task.WhenAll(testFeed.Entries.Select(entry =>
            mariaDbHandlerMock.Received(1)
                .InsertGistAsync(
                    Arg.Is<Gist>(gist => gist.Reference == entry.Reference && gist.Updated == entry.Updated),
                    Arg.Any<CancellationToken>())));
        await Task.WhenAll(testSearchResults.Select(searchResult =>
            mariaDbHandlerMock.InsertSearchResultsAsync(searchResult, Arg.Any<CancellationToken>())));
        await mariaDbHandlerMock.DidNotReceive().UpdateGistAsync(Arg.Any<Gist>(), Arg.Any<CancellationToken>());
        await mariaDbHandlerMock.DidNotReceive()
            .UpdateSearchResultsAsync(Arg.Any<IEnumerable<GoogleSearchResult>>(), Arg.Any<CancellationToken>());
    }

    private static IWebCrawlHandler CreateMockedRssEntryHandler(
        List<TestFeedData> testFeeds,
        List<string> testTexts
    )
    {
        var rssEntryHandlerMock = Substitute.For<IWebCrawlHandler>();
        var testEntries = testFeeds.SelectMany(feed => feed.Entries);
        foreach (var (entry, text) in testEntries.Zip(testTexts))
        {
            rssEntryHandlerMock.FetchPageContentAsync(entry.Url.AbsoluteUri).Returns(Task.FromResult(text));
        }
        return rssEntryHandlerMock;
    }

    private static IOpenAIHandler CreateMockedOpenAIHandler(
        List<TestFeedData> testFeeds,
        List<string> testTexts,
        List<SummaryAIResponse> testAIResponses
    )
    {
        var openAIHandlerMock = Substitute.For<IOpenAIHandler>();
        var testEntries = testFeeds.SelectMany(feed => feed.Entries);
        foreach (var (entry, text, aiResponse) in testEntries.Zip(testTexts, testAIResponses))
        {
            openAIHandlerMock
                .GenerateSummaryTagsAndQueryAsync(entry.Title, text, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(aiResponse));
        }
        return openAIHandlerMock;
    }

    private static IGoogleSearchHandler CreateMockedGoogleSearchHandler(
        List<SummaryAIResponse> testAIResponses,
        List<List<GoogleSearchResult>> testSearchResults
    )
    {
        var googleSearchHandlerMock = Substitute.For<IGoogleSearchHandler>();
        foreach (var (aiResponse, searchResults) in testAIResponses.Zip(testSearchResults))
        {
            googleSearchHandlerMock
                .GetSearchResultsAsync(aiResponse.SearchQuery, Arg.Any<int>(), Arg.Any<CancellationToken>())!
                .Returns(callInfo =>
                    Task.FromResult(
                        searchResults.Select(result => result with { GistId = callInfo.ArgAt<int>(1) }).ToList()
                    ));
        }
        return googleSearchHandlerMock;
    }

    private static GistService CreateGistService(
        List<TestFeedData> testFeeds,
        List<List<GoogleSearchResult>>? testSearchResults = null,
        List<string>? testTexts = null,
        IWebCrawlHandler? webCrawlHandler = null,
        IMariaDbHandler? mariaDbHandlerMock = null,
        IOpenAIHandler? openAIHandlerMock = null,
        IChromaDbHandler? chromaDbHandlerMock = null,
        IGoogleSearchHandler? googleSearchHandlerMock = null,
        ILogger<GistService>? loggerMock = null
    )
    {
        var entryCount = testFeeds.Select(feed => feed.Entries.Count).Sum();
        var testAIResponses = CreateTestSummaryAIResponses(entryCount);
        testTexts ??= CreateTestStrings(entryCount);
        return new GistService(
            CreateRssFeedHandler(CreateMockedHttpClient(testFeeds), testFeeds),
            webCrawlHandler ?? CreateMockedRssEntryHandler(testFeeds, testTexts),
            mariaDbHandlerMock ?? Substitute.For<IMariaDbHandler>(),
            openAIHandlerMock ?? CreateMockedOpenAIHandler(testFeeds, testTexts, testAIResponses),
            chromaDbHandlerMock ?? Substitute.For<IChromaDbHandler>(),
            googleSearchHandlerMock ?? CreateMockedGoogleSearchHandler(testAIResponses,
                testSearchResults ?? CreateMultipleTestSearchResults(entryCount)),
            loggerMock
        );
    }
}
