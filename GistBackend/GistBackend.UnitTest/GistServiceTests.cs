using GistBackend.Handlers;
using GistBackend.Handlers.ChromaDbHandler;
using GistBackend.Handlers.GoogleSearchHandler;
using GistBackend.Handlers.MariaDbHandler;
using GistBackend.Handlers.OpenAiHandler;
using GistBackend.Services;
using GistBackend.Types;
using Microsoft.Extensions.Logging;
using NSubstitute;
using static TestUtilities.TestData;

// ReSharper disable AsyncVoidLambda

namespace GistBackend.UnitTest;

public class GistServiceTests
{
    [Fact]
    public async Task StartAsync_FeedInfosDoNotExistInDb_FeedInfosAreInserted()
    {
        var testRssFeeds = CreateTestRssFeeds(5);
        var mariaDbHandlerMock = Substitute.For<IMariaDbHandler>();
        mariaDbHandlerMock
            .GetFeedInfoByRssUrlAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(null as RssFeedInfo));
        var gistService = CreateGistService(mariaDbHandlerMock: mariaDbHandlerMock, testRssFeeds: testRssFeeds);

        await gistService.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        foreach (var testRssFeed in testRssFeeds)
        {
            await mariaDbHandlerMock
                .Received(1)
                .InsertFeedInfoAsync(testRssFeed.ToRssFeedInfo(), Arg.Any<CancellationToken>());
        }
    }

    [Fact]
    public async Task StartAsync_DifferentFeedInfoExistsInDb_FeedInfoIsUpdated()
    {
        var oldFeed = CreateTestRssFeed();
        var newFeed = CreateTestRssFeed() with { Id = oldFeed.Id };
        var mariaDbHandlerMock = Substitute.For<IMariaDbHandler>();
        mariaDbHandlerMock
            .GetFeedInfoByRssUrlAsync(newFeed.RssUrl, Arg.Any<CancellationToken>())!
            .Returns(Task.FromResult(oldFeed.ToRssFeedInfo()));
        var gistService = CreateGistService(
            testRssFeeds: [ newFeed ],
            mariaDbHandlerMock: mariaDbHandlerMock
        );

        await gistService.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        await mariaDbHandlerMock.Received(1).UpdateFeedInfoAsync(newFeed.ToRssFeedInfo(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_TwoTestEntriesInFeed_EntriesProcessedFromOldestToNewest()
    {
        var testRssFeed = CreateTestRssFeed() with {
            Entries = CreateTestEntries(5).OrderByDescending(entry => entry.Updated)
        };
        var mariaDbHandlerMock = Substitute.For<IMariaDbHandler>();
        var gistService = CreateGistService(mariaDbHandlerMock: mariaDbHandlerMock, testRssFeeds: [ testRssFeed ]);

        await gistService.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        var orderedEntries = testRssFeed.Entries.OrderBy(entry => entry.Updated).ToArray();
        Received.InOrder(async () => {
            foreach (var entry in orderedEntries)
                await mariaDbHandlerMock.GetGistByReferenceAsync(entry.Reference, Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task StartAsync_EntryAlreadyExistInDbButSearchResultsDoNotForSomeGists_SearchResultsAreRequestedAndInserted()
    {
        var rssFeed = CreateTestRssFeed();
        var gists =
            rssFeed.Entries.Select(entry => CreateTestGist(entry.FeedId, entry.Reference, entry.Updated)).ToList();
        var gistsWithSearchResults = gists.Skip(2).ToList();
        var gistsWithoutSearchResults = gists.Take(2).ToList();
        var searchResults = gists.Select(gist => CreateTestSearchResults(10, gist.Id!.Value)).ToList();
        var existingSearchResults = searchResults.Skip(2).ToList();
        var missingSearchResults = searchResults.Take(2).ToList();
        var mariaDbHandlerMock = Substitute.For<IMariaDbHandler>();
        gists.ForEach(gist =>
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
            testRssFeeds: [rssFeed],
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
        var rssFeed = CreateTestRssFeed();
        var gists =
            rssFeed.Entries.Select(entry => CreateTestGist(entry.FeedId, entry.Reference, entry.Updated)).ToList();
        var testSearchResults = gists.Select(gist => CreateTestSearchResults(10, gist.Id!.Value)).ToList();
        var mariaDbHandlerMock = Substitute.For<IMariaDbHandler>();
        gists.ForEach(gist =>
            mariaDbHandlerMock.GetGistByReferenceAsync(gist.Reference, Arg.Any<CancellationToken>())!
                .Returns(Task.FromResult(gist))
        );
        foreach (var (gist, searchResults) in gists.Zip(testSearchResults))
        {
            mariaDbHandlerMock
                .GetSearchResultsByGistIdAsync(gist.Id!.Value, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(searchResults));
        }
        var googleSearchHandlerMock = Substitute.For<IGoogleSearchHandler>();
        var gistService = CreateGistService(
            testRssFeeds: [rssFeed],
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
        var rssFeed = CreateTestRssFeed();
        var gists =
            rssFeed.Entries.Select(entry => CreateTestGist(entry.FeedId, entry.Reference, entry.Updated)).ToList();
        var testSearchResults = gists.Select(gist => CreateTestSearchResults(10, gist.Id!.Value)).ToList();
        var mariaDbHandlerMock = Substitute.For<IMariaDbHandler>();
        gists.ForEach(gist =>
            mariaDbHandlerMock.GetGistByReferenceAsync(gist.Reference, Arg.Any<CancellationToken>())!
                .Returns(Task.FromResult(gist with { Updated = gist.Updated.AddDays(-5)}))
        );
        var chromaDbHandlerMock = Substitute.For<IChromaDbHandler>();
        var googleSearchHandlerMock = Substitute.For<IGoogleSearchHandler>();
        foreach (var (gist, searchResults) in gists.Zip(testSearchResults))
        {
            googleSearchHandlerMock.GetSearchResultsAsync(gist.SearchQuery, gist.Id!.Value,
                Arg.Any<CancellationToken>())!.Returns(Task.FromResult(searchResults));
        }
        var gistService = CreateGistService(
            testRssFeeds: [rssFeed],
            mariaDbHandlerMock: mariaDbHandlerMock,
            chromaDbHandlerMock: chromaDbHandlerMock,
            googleSearchHandlerMock: googleSearchHandlerMock
        );

        await gistService.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        await Task.WhenAll(rssFeed.Entries.Select(entry =>
            chromaDbHandlerMock.Received(1).InsertEntryAsync(entry, Arg.Any<string>(), Arg.Any<CancellationToken>())));
        await Task.WhenAll(rssFeed.Entries.Select(entry =>
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
        var rssFeed = CreateTestRssFeed();
        var searchResults = rssFeed.Entries.Select(_ => CreateTestSearchResults(10)).ToList();
        var texts = CreateTestStrings(rssFeed.Entries.Count());
        var aiResponses = CreateTestSummaryAIResponses(rssFeed.Entries.Count());
        var mariaDbHandlerMock = Substitute.For<IMariaDbHandler>();
        var chromaDbHandlerMock = Substitute.For<IChromaDbHandler>();
        var googleSearchHandlerMock = Substitute.For<IGoogleSearchHandler>();
        foreach (var (aiResponse, searchResult) in aiResponses.Zip(searchResults))
        {
            googleSearchHandlerMock.GetSearchResultsAsync(aiResponse.SearchQuery, Arg.Any<int>(),
                Arg.Any<CancellationToken>())!.Returns(callInfo =>
                Task.FromResult(
                    searchResult.Select(result => result with { GistId = callInfo.ArgAt<int>(1) }).ToList()
                ));
        }
        var gistService = CreateGistService(
            mariaDbHandlerMock: mariaDbHandlerMock,
            chromaDbHandlerMock: chromaDbHandlerMock,
            googleSearchHandlerMock: googleSearchHandlerMock,
            testRssFeeds: [rssFeed],
            testTexts: texts,
            testAIResponses: aiResponses
        );

        await gistService.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        foreach (var (entry, text) in rssFeed.Entries.Zip(texts))
        {
            await chromaDbHandlerMock.Received(1).InsertEntryAsync(entry, text, Arg.Any<CancellationToken>());
        }

        await Task.WhenAll(rssFeed.Entries.Select(entry =>
            mariaDbHandlerMock.Received(1)
                .InsertGistAsync(
                    Arg.Is<Gist>(gist => gist.Reference == entry.Reference && gist.Updated == entry.Updated),
                    Arg.Any<CancellationToken>())));
        await Task.WhenAll(searchResults.Select(searchResult =>
            mariaDbHandlerMock.InsertSearchResultsAsync(searchResult, Arg.Any<CancellationToken>())));
        await mariaDbHandlerMock.DidNotReceive().UpdateGistAsync(Arg.Any<Gist>(), Arg.Any<CancellationToken>());
        await mariaDbHandlerMock.DidNotReceive()
            .UpdateSearchResultsAsync(Arg.Any<IEnumerable<GoogleSearchResult>>(), Arg.Any<CancellationToken>());
    }

    private static GistService CreateGistService(
        IRssFeedHandler? rssFeedHandlerMock = null,
        IRssEntryHandler? rssEntryHandlerMock = null,
        IMariaDbHandler? mariaDbHandlerMock = null,
        IOpenAIHandler? openAIHandlerMock = null,
        IChromaDbHandler? chromaDbHandlerMock = null,
        IGoogleSearchHandler? googleSearchHandlerMock = null,
        ILogger<GistService>? loggerMock = null,
        List<RssFeed>? testRssFeeds = null,
        List<SummaryAIResponse>? testAIResponses = null,
        List<string>? testTexts = null
    )
    {
        testRssFeeds ??= CreateTestRssFeeds(5);
        var entryCount = testRssFeeds.Select(feed => feed.Entries.Count()).Sum();
        testAIResponses ??= CreateTestSummaryAIResponses(entryCount);
        testTexts ??= CreateTestStrings(entryCount);
        if (rssFeedHandlerMock is null)
        {
            rssFeedHandlerMock = Substitute.For<IRssFeedHandler>();
            rssFeedHandlerMock.Definitions.Returns(testRssFeeds);
        }
        if (rssEntryHandlerMock is null)
        {
            rssEntryHandlerMock = Substitute.For<IRssEntryHandler>();
            var textIndex = 0;
            foreach (var rssFeed in testRssFeeds)
            {
                rssFeed.Entries.ToList().ForEach(entry => rssEntryHandlerMock.FetchTextContentAsync(entry, Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult(testTexts[textIndex++])));
            }
        }
        if (openAIHandlerMock is null)
        {
            openAIHandlerMock = Substitute.For<IOpenAIHandler>();
            var textIndex = 0;
            foreach (var entry in testRssFeeds.SelectMany(rssFeed => rssFeed.Entries))
            {
                openAIHandlerMock
                    .GenerateSummaryTagsAndQueryAsync(entry.Title, testTexts[textIndex],
                        Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult(testAIResponses[textIndex]));
                textIndex += 1;
            }
        }
        return new GistService(
            rssFeedHandlerMock,
            rssEntryHandlerMock,
            mariaDbHandlerMock ?? Substitute.For<IMariaDbHandler>(),
            openAIHandlerMock,
            chromaDbHandlerMock ?? Substitute.For<IChromaDbHandler>(),
            googleSearchHandlerMock ?? Substitute.For<IGoogleSearchHandler>(),
            loggerMock
        );
    }
}
