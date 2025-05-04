using GistBackend.Handler;
using GistBackend.Handler.ChromaDbHandler;
using GistBackend.Handler.GoogleSearchHandler;
using GistBackend.Handler.MariaDbHandler;
using GistBackend.Handler.OpenAiHandler;
using GistBackend.Services;
using GistBackend.Types;
using Microsoft.Extensions.Logging;
using NSubstitute;
using static GistBackend.UnitTest.Utils.TestData;

// ReSharper disable AsyncVoidLambda

namespace GistBackend.UnitTest;

public class GistServiceTests
{
    [Fact]
    public async Task StartAsync_FeedInfosDoNotExistInDb_FeedInfosAreInserted()
    {
        var mariaDbHandlerMock = Substitute.For<IMariaDbHandler>();
        mariaDbHandlerMock
            .GetFeedInfoByRssUrlAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(null as RssFeedInfo));
        var gistService = CreateGistService(mariaDbHandlerMock: mariaDbHandlerMock);

        await gistService.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        await mariaDbHandlerMock.Received(1)
            .InsertFeedInfoAsync(_testRssFeeds.First().ToRssFeedInfo(), Arg.Any<CancellationToken>());
        await mariaDbHandlerMock.Received(1)
            .InsertFeedInfoAsync(_testRssFeeds.Last().ToRssFeedInfo(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_DifferentFeedInfoExistsInDb_FeedInfoIsUpdated()
    {
        var oldFeed = _testRssFeeds.First();
        var newFeed = _testRssFeeds.Last() with { Id = oldFeed.Id };
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

    [Fact]
    public async Task StartAsync_TwoTestEntriesInFeed_EntriesProcessedFromOldestToNewest()
    {
        var mariaDbHandlerMock = Substitute.For<IMariaDbHandler>();
        var gistService = CreateGistService(mariaDbHandlerMock: mariaDbHandlerMock);

        await gistService.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        var orderedEntries = TestRssEntries.OrderBy(entry => entry.Updated).ToArray();
        Received.InOrder(async () => {
            await mariaDbHandlerMock.GetGistByReferenceAsync(orderedEntries.First().Reference, Arg.Any<CancellationToken>());
            await mariaDbHandlerMock.GetGistByReferenceAsync(orderedEntries.Last().Reference, Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task StartAsync_TwoTestEntriesReversedInFeed_EntriesProcessedFromOldestToNewest()
    {
        var rssFeedHandlerMock = Substitute.For<IRssFeedHandler>();
        var reversedEntries = TestRssEntries.OrderBy(entry => entry.Updated).Reverse();
        var rssFeeds = new List<RssFeed> { _testRssFeeds.First() with { Entries = reversedEntries } };
        rssFeedHandlerMock.Definitions.Returns(rssFeeds);
        var mariaDbHandlerMock = Substitute.For<IMariaDbHandler>();
        var gistService = CreateGistService(mariaDbHandlerMock: mariaDbHandlerMock);

        await gistService.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        var orderedEntries = TestRssEntries.OrderBy(entry => entry.Updated).ToArray();
        Received.InOrder(async () => {
            await mariaDbHandlerMock.GetGistByReferenceAsync(orderedEntries.First().Reference, Arg.Any<CancellationToken>());
            await mariaDbHandlerMock.GetGistByReferenceAsync(orderedEntries.Last().Reference, Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task StartAsync_EntryAlreadyExistInDbButSearchResultsAreNotForOneGist_SearchResultIsRequestedAndInsertedForGist()
    {
        var mariaDbHandlerMock = Substitute.For<IMariaDbHandler>();
        TestGists.ForEach(gist =>
            mariaDbHandlerMock.GetGistByReferenceAsync(gist.Reference, Arg.Any<CancellationToken>())!
                .Returns(Task.FromResult(gist))
        );
        mariaDbHandlerMock
            .GetSearchResultsByGistIdAsync(TestSearchResults.First().First().GistId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TestSearchResults.First()));
        mariaDbHandlerMock
            .GetSearchResultsByGistIdAsync(TestSearchResults.Last().First().GistId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<GoogleSearchResult>()));
        var googleSearchHandlerMock = Substitute.For<IGoogleSearchHandler>();
        googleSearchHandlerMock
            .GetSearchResultsAsync(TestGists.Last().SearchQuery, TestGists.Last().Id!.Value,
                Arg.Any<CancellationToken>())!.Returns(Task.FromResult(TestSearchResults.Last()));
        var gistService = CreateGistService(
            mariaDbHandlerMock: mariaDbHandlerMock,
            googleSearchHandlerMock: googleSearchHandlerMock
        );

        await gistService.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        await googleSearchHandlerMock.Received()
            .GetSearchResultsAsync(TestGists.Last().SearchQuery, TestGists.Last().Id!.Value,
                Arg.Any<CancellationToken>());
        await mariaDbHandlerMock.Received()
            .InsertSearchResultsAsync(TestSearchResults.Last(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_EntryAndSearchResultAlreadyExistInDb_SearchResultIsNeitherRequestedNorInserted()
    {
        var mariaDbHandlerMock = Substitute.For<IMariaDbHandler>();
        TestGists.ForEach(gist =>
            mariaDbHandlerMock.GetGistByReferenceAsync(gist.Reference, Arg.Any<CancellationToken>())!
                .Returns(Task.FromResult(gist))
        );
        TestSearchResults.ForEach(searchResults =>
            mariaDbHandlerMock.GetSearchResultsByGistIdAsync(searchResults.First().GistId, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(searchResults))
        );
        var googleSearchHandlerMock = Substitute.For<IGoogleSearchHandler>();
        var gistService = CreateGistService(
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
    public async Task StartAsync_OldVersionOfGistExists_GistIsGeneratedAndInserted()
    {
        var mariaDbHandlerMock = Substitute.For<IMariaDbHandler>();
        TestGists.ForEach(gist =>
            mariaDbHandlerMock.GetGistByReferenceAsync(gist.Reference, Arg.Any<CancellationToken>())!
                .Returns(Task.FromResult(gist with { Updated = gist.Updated.AddDays(-5)}))
        );
        var chromaDbHandlerMock = Substitute.For<IChromaDbHandler>();
        var googleSearchHandlerMock = Substitute.For<IGoogleSearchHandler>();
        foreach (var (gist, searchResults) in TestGists.Zip(TestSearchResults))
        {
            googleSearchHandlerMock.GetSearchResultsAsync(gist.SearchQuery, gist.Id!.Value,
                Arg.Any<CancellationToken>())!.Returns(Task.FromResult(searchResults));
        }
        var gistService = CreateGistService(
            mariaDbHandlerMock: mariaDbHandlerMock,
            chromaDbHandlerMock: chromaDbHandlerMock,
            googleSearchHandlerMock: googleSearchHandlerMock
        );

        await gistService.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        foreach (var (entry, text) in TestRssEntries.Zip(TestTexts))
        {
            await chromaDbHandlerMock.Received(1).InsertEntryAsync(entry, text, Arg.Any<CancellationToken>());
        }
        await Task.WhenAll(TestGists.Select(gist =>
            mariaDbHandlerMock.Received(1).UpdateGistAsync(gist with { Id = null }, Arg.Any<CancellationToken>())));
        await Task.WhenAll(TestSearchResults.Select(searchResults =>
            mariaDbHandlerMock.UpdateSearchResultsAsync(searchResults, Arg.Any<CancellationToken>())));
        await mariaDbHandlerMock.DidNotReceive().InsertGistAsync(Arg.Any<Gist>(), Arg.Any<CancellationToken>());
        await mariaDbHandlerMock.DidNotReceive()
            .InsertSearchResultsAsync(Arg.Any<IEnumerable<GoogleSearchResult>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_GistDoesNotExist_GistIsGeneratedAndInserted()
    {
        var mariaDbHandlerMock = Substitute.For<IMariaDbHandler>();
        var chromaDbHandlerMock = Substitute.For<IChromaDbHandler>();
        var googleSearchHandlerMock = Substitute.For<IGoogleSearchHandler>();
        foreach (var (gist, searchResults) in TestGists.Zip(TestSearchResults))
        {
            googleSearchHandlerMock.GetSearchResultsAsync(gist.SearchQuery, gist.Id!.Value,
                Arg.Any<CancellationToken>())!.Returns(Task.FromResult(searchResults));
        }
        var gistService = CreateGistService(
            mariaDbHandlerMock: mariaDbHandlerMock,
            chromaDbHandlerMock: chromaDbHandlerMock,
            googleSearchHandlerMock: googleSearchHandlerMock
        );

        await gistService.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        foreach (var (entry, text) in TestRssEntries.Zip(TestTexts))
        {
            await chromaDbHandlerMock.Received(1).InsertEntryAsync(entry, text, Arg.Any<CancellationToken>());
        }
        await Task.WhenAll(TestGists.Select(gist =>
            mariaDbHandlerMock.Received(1).InsertGistAsync(gist with { Id = null }, Arg.Any<CancellationToken>())));
        await Task.WhenAll(TestSearchResults.Select(searchResults =>
            mariaDbHandlerMock.InsertSearchResultsAsync(searchResults, Arg.Any<CancellationToken>())));
        await mariaDbHandlerMock.DidNotReceive().UpdateGistAsync(Arg.Any<Gist>(), Arg.Any<CancellationToken>());
        await mariaDbHandlerMock.DidNotReceive()
            .UpdateSearchResultsAsync(Arg.Any<IEnumerable<GoogleSearchResult>>(), Arg.Any<CancellationToken>());
    }

    private readonly List<RssFeed> _testRssFeeds = [
        new("test url", content => content) {
            Id = 1,
            Title = "test title",
            Language = "test language",
            Entries = TestRssEntries
        },
        new("another test url", content => content) {
            Id = 2,
            Title = "another test title",
            Language = "another test language"
        }
    ];

    private static readonly List<string> TestTexts = [ "test text", "another test text" ];

    private static readonly List<List<GoogleSearchResult>> TestSearchResults = TestGists.Select((gist, i) =>
        new List<GoogleSearchResult> { new(
            gist.Id!.Value,
            $"search title for {gist.Title}",
            $"search snippet for {gist.Title}",
            $"search link for {gist.Title}",
            $"search display link for {gist.Title}",
            $"search thumbnail link for {gist.Title}",
            i
        ) }
    ).ToList();

    private GistService CreateGistService(
        IRssFeedHandler? rssFeedHandlerMock = null,
        IRssEntryHandler? rssEntryHandlerMock = null,
        IMariaDbHandler? mariaDbHandlerMock = null,
        IOpenAIHandler? openAIHandlerMock = null,
        IChromaDbHandler? chromaDbHandlerMock = null,
        IGoogleSearchHandler? googleSearchHandlerMock = null,
        ILogger<GistService>? loggerMock = null
    )
    {
        if (rssFeedHandlerMock is null)
        {
            rssFeedHandlerMock = Substitute.For<IRssFeedHandler>();
            rssFeedHandlerMock.Definitions.Returns(_testRssFeeds);
        }
        if (rssEntryHandlerMock is null)
        {
            rssEntryHandlerMock = Substitute.For<IRssEntryHandler>();
            foreach (var (rssEntry, text) in TestRssEntries.Zip(TestTexts))
            {
                rssEntryHandlerMock.FetchTextContentAsync(rssEntry, Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult(text));
            }
        }
        if (openAIHandlerMock is null)
        {
            openAIHandlerMock = Substitute.For<IOpenAIHandler>();
            foreach (var (rssEntry, text, aiResponse) in TestRssEntries.Zip(TestTexts, TestAIResponses))
            {
                openAIHandlerMock.GenerateSummaryTagsAndQueryAsync(rssEntry.Title, text, Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult(aiResponse));
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
