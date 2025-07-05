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
        var rssFeedHandlerMock = Substitute.For<IRssFeedHandler>();
        rssFeedHandlerMock.Definitions.Returns([ newFeed ]);
        var mariaDbHandlerMock = Substitute.For<IMariaDbHandler>();
        mariaDbHandlerMock
            .GetFeedInfoByRssUrlAsync(newFeed.RssUrl, Arg.Any<CancellationToken>())!
            .Returns(Task.FromResult(oldFeed.ToRssFeedInfo()));
        var gistService = CreateGistService(
            rssFeedHandlerMock: rssFeedHandlerMock,
            mariaDbHandlerMock: mariaDbHandlerMock
        );

        await gistService.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        await mariaDbHandlerMock.Received(1).UpdateFeedInfoAsync(newFeed.ToRssFeedInfo(), Arg.Any<CancellationToken>());
    }

    // [Fact]
    // public async Task StartAsync_TwoTestEntriesInFeed_EntriesProcessedFromOldestToNewest()
    // {
    //     var testRssEntries = CreateTestEntries(5);
    //     var mariaDbHandlerMock = Substitute.For<IMariaDbHandler>();
    //     var gistService = CreateGistService(mariaDbHandlerMock: mariaDbHandlerMock, testRssEntries: testRssEntries);
    //
    //     await gistService.StartAsync(CancellationToken.None);
    //     await Task.Delay(TimeSpan.FromSeconds(2));
    //
    //     var orderedEntries = testRssEntries.OrderBy(entry => entry.Updated).ToArray();
    //     Received.InOrder(async () => {
    //         foreach (var entry in orderedEntries)
    //             await mariaDbHandlerMock.GetGistByReferenceAsync(entry.Reference, Arg.Any<CancellationToken>());
    //     });
    // }
    //
    // [Fact]
    // public async Task StartAsync_TwoTestEntriesReversedInFeed_EntriesProcessedFromOldestToNewest()
    // {
    //     var testRssEntries = CreateTestEntries(5);
    //     var rssFeedHandlerMock = Substitute.For<IRssFeedHandler>();
    //     var reversedEntries = testRssEntries.OrderBy(entry => entry.Updated).Reverse();
    //     var rssFeeds = new List<RssFeed> { CreateTestRssFeed() with { Entries = reversedEntries } };
    //     rssFeedHandlerMock.Definitions.Returns(rssFeeds);
    //     var mariaDbHandlerMock = Substitute.For<IMariaDbHandler>();
    //     var gistService = CreateGistService(mariaDbHandlerMock: mariaDbHandlerMock);
    //
    //     await gistService.StartAsync(CancellationToken.None);
    //     await Task.Delay(TimeSpan.FromSeconds(2));
    //
    //     var orderedEntries = testRssEntries.OrderBy(entry => entry.Updated).ToArray();
    //     Received.InOrder(async () => {
    //         foreach (var entry in orderedEntries)
    //             await mariaDbHandlerMock.GetGistByReferenceAsync(entry.Reference, Arg.Any<CancellationToken>());
    //     });
    // }
    //
    // [Fact]
    // public async Task StartAsync_EntryAlreadyExistInDbButSearchResultsAreNotForOneGist_SearchResultIsRequestedAndInsertedForGist()
    // {
    //     var testGists = CreateTestGists(5);
    //     var testSearchResults = testGists.Select(gist => CreateTestSearchResults(10, gist.Id!.Value)).ToList();
    //     var gistWithoutSearchResults = CreateTestGist();
    //     var missingSearchResults = CreateTestSearchResults(10, gistWithoutSearchResults.Id!.Value);
    //     var mariaDbHandlerMock = Substitute.For<IMariaDbHandler>();
    //     testGists.ForEach(gist =>
    //         mariaDbHandlerMock.GetGistByReferenceAsync(gist.Reference, Arg.Any<CancellationToken>())!
    //             .Returns(Task.FromResult(gist))
    //     );
    //     foreach (var (gist, searchResults) in testGists.Zip(testSearchResults))
    //     {
    //         mariaDbHandlerMock
    //             .GetSearchResultsByGistIdAsync(gist.Id!.Value, Arg.Any<CancellationToken>())
    //             .Returns(Task.FromResult(searchResults));
    //     }
    //     mariaDbHandlerMock
    //         .GetSearchResultsByGistIdAsync(gistWithoutSearchResults.Id!.Value, Arg.Any<CancellationToken>())
    //         .Returns(Task.FromResult(new List<GoogleSearchResult>()));
    //     var googleSearchHandlerMock = Substitute.For<IGoogleSearchHandler>();
    //     googleSearchHandlerMock
    //         .GetSearchResultsAsync(testGists.Last().SearchQuery, testGists.Last().Id!.Value,
    //             Arg.Any<CancellationToken>())!.Returns(Task.FromResult(missingSearchResults));
    //     var gistService = CreateGistService(
    //         mariaDbHandlerMock: mariaDbHandlerMock,
    //         googleSearchHandlerMock: googleSearchHandlerMock
    //     );
    //
    //     await gistService.StartAsync(CancellationToken.None);
    //     await Task.Delay(TimeSpan.FromSeconds(2));
    //
    //     await googleSearchHandlerMock.Received()
    //         .GetSearchResultsAsync(gistWithoutSearchResults.SearchQuery, gistWithoutSearchResults.Id!.Value,
    //             Arg.Any<CancellationToken>());
    //     await mariaDbHandlerMock.Received()
    //         .InsertSearchResultsAsync(missingSearchResults, Arg.Any<CancellationToken>());
    // }
    //
    // [Fact]
    // public async Task StartAsync_EntryAndSearchResultAlreadyExistInDb_SearchResultIsNeitherRequestedNorInserted()
    // {
    //     var testGists = CreateTestGists(5);
    //     var testSearchResults = testGists.Select(gist => CreateTestSearchResults(10, gist.Id!.Value)).ToList();
    //     var mariaDbHandlerMock = Substitute.For<IMariaDbHandler>();
    //     testGists.ForEach(gist =>
    //         mariaDbHandlerMock.GetGistByReferenceAsync(gist.Reference, Arg.Any<CancellationToken>())!
    //             .Returns(Task.FromResult(gist))
    //     );
    //     foreach (var (gist, searchResults) in testGists.Zip(testSearchResults))
    //     {
    //         mariaDbHandlerMock
    //             .GetSearchResultsByGistIdAsync(gist.Id!.Value, Arg.Any<CancellationToken>())
    //             .Returns(Task.FromResult(searchResults));
    //     }
    //     var googleSearchHandlerMock = Substitute.For<IGoogleSearchHandler>();
    //     var gistService = CreateGistService(
    //         mariaDbHandlerMock: mariaDbHandlerMock,
    //         googleSearchHandlerMock: googleSearchHandlerMock
    //     );
    //
    //     await gistService.StartAsync(CancellationToken.None);
    //     await Task.Delay(TimeSpan.FromSeconds(2));
    //
    //     await googleSearchHandlerMock.DidNotReceive()
    //         .GetSearchResultsAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    //     await mariaDbHandlerMock.DidNotReceive()
    //         .InsertSearchResultsAsync(Arg.Any<IEnumerable<GoogleSearchResult>>(), Arg.Any<CancellationToken>());
    // }
    //
    // [Fact]
    // public async Task StartAsync_OldVersionOfGistExists_GistIsGeneratedAndInserted()
    // {
    //     var testRssEntries = CreateTestEntries(5);
    //     var testTexts = CreateTestStrings(testRssEntries.Count);
    //     var testGists = CreateTestGists(testRssEntries.Count);
    //     var testSearchResults = testGists.Select(gist => CreateTestSearchResults(10, gist.Id!.Value)).ToList();
    //     var mariaDbHandlerMock = Substitute.For<IMariaDbHandler>();
    //     testGists.ForEach(gist =>
    //         mariaDbHandlerMock.GetGistByReferenceAsync(gist.Reference, Arg.Any<CancellationToken>())!
    //             .Returns(Task.FromResult(gist with { Updated = gist.Updated.AddDays(-5)}))
    //     );
    //     var chromaDbHandlerMock = Substitute.For<IChromaDbHandler>();
    //     var googleSearchHandlerMock = Substitute.For<IGoogleSearchHandler>();
    //     foreach (var (gist, searchResults) in testGists.Zip(testSearchResults))
    //     {
    //         googleSearchHandlerMock.GetSearchResultsAsync(gist.SearchQuery, gist.Id!.Value,
    //             Arg.Any<CancellationToken>())!.Returns(Task.FromResult(searchResults));
    //     }
    //     var gistService = CreateGistService(
    //         mariaDbHandlerMock: mariaDbHandlerMock,
    //         chromaDbHandlerMock: chromaDbHandlerMock,
    //         googleSearchHandlerMock: googleSearchHandlerMock,
    //         testRssEntries: testRssEntries
    //     );
    //
    //     await gistService.StartAsync(CancellationToken.None);
    //     await Task.Delay(TimeSpan.FromSeconds(2));
    //
    //     foreach (var (entry, text) in testRssEntries.Zip(testTexts))
    //         await chromaDbHandlerMock.Received(1).InsertEntryAsync(entry, text, Arg.Any<CancellationToken>());
    //     await Task.WhenAll(testGists.Select(gist =>
    //         mariaDbHandlerMock.Received(1).UpdateGistAsync(gist with { Id = null }, Arg.Any<CancellationToken>())));
    //     await Task.WhenAll(testSearchResults.Select(searchResults =>
    //         mariaDbHandlerMock.UpdateSearchResultsAsync(searchResults, Arg.Any<CancellationToken>())));
    //     await mariaDbHandlerMock.DidNotReceive().InsertGistAsync(Arg.Any<Gist>(), Arg.Any<CancellationToken>());
    //     await mariaDbHandlerMock.DidNotReceive()
    //         .InsertSearchResultsAsync(Arg.Any<IEnumerable<GoogleSearchResult>>(), Arg.Any<CancellationToken>());
    // }
    //
    // [Fact]
    // public async Task StartAsync_GistDoesNotExist_GistIsGeneratedAndInserted()
    // {
    //     var testGists = CreateTestGists(5);
    //     var testSearchResults = testGists.Select(gist => CreateTestSearchResults(10, gist.Id!.Value)).ToList();
    //     var testRssEntries = CreateTestEntries(testGists.Count);
    //     var testTexts = CreateTestStrings(testRssEntries.Count);
    //     var mariaDbHandlerMock = Substitute.For<IMariaDbHandler>();
    //     var chromaDbHandlerMock = Substitute.For<IChromaDbHandler>();
    //     var googleSearchHandlerMock = Substitute.For<IGoogleSearchHandler>();
    //     foreach (var (gist, searchResults) in testGists.Zip(testSearchResults))
    //     {
    //         googleSearchHandlerMock.GetSearchResultsAsync(gist.SearchQuery, gist.Id!.Value,
    //             Arg.Any<CancellationToken>())!.Returns(Task.FromResult(searchResults));
    //     }
    //     var gistService = CreateGistService(
    //         mariaDbHandlerMock: mariaDbHandlerMock,
    //         chromaDbHandlerMock: chromaDbHandlerMock,
    //         googleSearchHandlerMock: googleSearchHandlerMock,
    //         testRssEntries: testRssEntries,
    //         testTexts: testTexts
    //     );
    //
    //     await gistService.StartAsync(CancellationToken.None);
    //     await Task.Delay(TimeSpan.FromSeconds(2));
    //
    //     foreach (var (entry, text) in testRssEntries.Zip(testTexts))
    //     {
    //         await chromaDbHandlerMock.Received(1).InsertEntryAsync(entry, text, Arg.Any<CancellationToken>());
    //     }
    //     await Task.WhenAll(testGists.Select(gist =>
    //         mariaDbHandlerMock.Received(1).InsertGistAsync(gist with { Id = null }, Arg.Any<CancellationToken>())));
    //     await Task.WhenAll(testSearchResults.Select(searchResults =>
    //         mariaDbHandlerMock.InsertSearchResultsAsync(searchResults, Arg.Any<CancellationToken>())));
    //     await mariaDbHandlerMock.DidNotReceive().UpdateGistAsync(Arg.Any<Gist>(), Arg.Any<CancellationToken>());
    //     await mariaDbHandlerMock.DidNotReceive()
    //         .UpdateSearchResultsAsync(Arg.Any<IEnumerable<GoogleSearchResult>>(), Arg.Any<CancellationToken>());
    // }

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
            foreach (var rssFeed in testRssFeeds)
            {
                rssFeed.Entries.ToList().ForEach(entry => openAIHandlerMock
                    .GenerateSummaryTagsAndQueryAsync(entry.Title, testTexts[textIndex++], Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult(testAIResponses[textIndex++])));
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
