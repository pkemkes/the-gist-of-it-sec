using GistBackend.Handlers;
using GistBackend.Handlers.ChromaDbHandler;
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
        var mariaDbHandlerMock = CreateMariaDbHandlerMock(testFeeds);
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
        var testFeeds = new List<TestFeedData> { oldFeed, newFeed };
        var mariaDbHandlerMock = CreateMariaDbHandlerMock(testFeeds);
        mariaDbHandlerMock
            .GetFeedInfoByRssUrlAsync(newFeed.RssFeed.RssUrl, Arg.Any<CancellationToken>())!
            .Returns(Task.FromResult(oldFeed.RssFeedInfo));
        var gistService = CreateGistService(
            testFeeds: testFeeds,
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
        var mariaDbHandlerMock = CreateMariaDbHandlerMock([testFeed]);
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
    public async Task StartAsync_OldVersionOfGistsExist_GistsAreGeneratedAndUpdated()
    {
        var testFeed = new TestFeedData(feedId: 0);
        var mariaDbHandlerMock = CreateMariaDbHandlerMock([testFeed]);
        testFeed.Gists.ForEach(gist =>
            mariaDbHandlerMock.GetGistByReferenceAsync(gist.Reference, Arg.Any<CancellationToken>())!
                .Returns(Task.FromResult(gist with { Updated = gist.Updated.AddDays(-5)}))
        );
        var chromaDbHandlerMock = Substitute.For<IChromaDbHandler>();
        var gistService = CreateGistService(
            testFeeds: [testFeed],
            mariaDbHandlerMock: mariaDbHandlerMock,
            chromaDbHandlerMock: chromaDbHandlerMock
        );

        await gistService.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        foreach (var (entry, summaryAIResponse) in testFeed.Entries.Zip(testFeed.SummaryAIResponses))
        {
            await chromaDbHandlerMock.Received(1).UpsertEntryAsync(
                Arg.Is<RssEntry>(e => e.Reference == entry.Reference && e.FeedId == entry.FeedId),
                Arg.Any<string>(), Arg.Any<CancellationToken>());
            var gistId = testFeed.Gists.First(gist => gist.Reference == entry.Reference).Id!.Value;
            var feedLanguage = testFeed.RssFeed.Language;
            await mariaDbHandlerMock.Received(1)
                .UpdateGistAsync(
                    Arg.Is<Gist>(gist => gist.Reference == entry.Reference && gist.Updated == entry.Updated),
                    Arg.Any<TransactionHandle>(), Arg.Any<CancellationToken>());
            var summary = new Summary(gistId, feedLanguage, false, entry.Title,
                feedLanguage == Language.En ? summaryAIResponse.SummaryEnglish : summaryAIResponse.SummaryGerman);
            await mariaDbHandlerMock.Received(1)
                .UpdateSummaryAsync(summary, Arg.Any<TransactionHandle>(), Arg.Any<CancellationToken>());
            var translatedSummary = new Summary(gistId, feedLanguage.Invert(), true,
                summaryAIResponse.TitleTranslated,
                feedLanguage == Language.En ? summaryAIResponse.SummaryGerman : summaryAIResponse.SummaryEnglish);
            await mariaDbHandlerMock.Received(1).UpdateSummaryAsync(translatedSummary, Arg.Any<TransactionHandle>(),
                Arg.Any<CancellationToken>());
        }
        await mariaDbHandlerMock.DidNotReceive()
            .InsertGistAsync(Arg.Any<Gist>(), Arg.Any<TransactionHandle>(), Arg.Any<CancellationToken>());
        await mariaDbHandlerMock.DidNotReceive().InsertSummaryAsync(Arg.Any<Summary>(), Arg.Any<TransactionHandle>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_GistDoesNotExist_GistIsGeneratedAndInserted()
    {
        var testFeed = new TestFeedData(feedId: 0);
        var mariaDbHandlerMock = CreateMariaDbHandlerMock([testFeed]);
        var chromaDbHandlerMock = Substitute.For<IChromaDbHandler>();
        var gistService = CreateGistService(
            mariaDbHandlerMock: mariaDbHandlerMock,
            chromaDbHandlerMock: chromaDbHandlerMock,
            testFeeds: [testFeed]
        );

        await gistService.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        foreach (var ((entry, text, summaryAIResponse), gist) in testFeed.Entries
                     .Zip(testFeed.Texts, testFeed.SummaryAIResponses).Zip(testFeed.Gists))
        {
            var feedLanguage = testFeed.RssFeed.Language;
            await chromaDbHandlerMock.Received(1)
                .UpsertEntryAsync(Arg.Is<RssEntry>(e => e.Reference == entry.Reference && e.FeedId == entry.FeedId),
                    text, Arg.Any<CancellationToken>());
            await mariaDbHandlerMock.Received(1)
                .InsertGistAsync(gist with { Id = null }, Arg.Any<TransactionHandle>(), Arg.Any<CancellationToken>());
            var summary = new Summary(gist.Id!.Value, feedLanguage, false, entry.Title,
                feedLanguage == Language.En ? summaryAIResponse.SummaryEnglish : summaryAIResponse.SummaryGerman);
            await mariaDbHandlerMock.Received(1)
                .InsertSummaryAsync(summary, Arg.Any<TransactionHandle>(), Arg.Any<CancellationToken>());
            var translatedSummary = new Summary(gist.Id!.Value, feedLanguage.Invert(), true,
                summaryAIResponse.TitleTranslated,
                feedLanguage == Language.En ? summaryAIResponse.SummaryGerman : summaryAIResponse.SummaryEnglish);
            await mariaDbHandlerMock.Received(1).InsertSummaryAsync(translatedSummary, Arg.Any<TransactionHandle>(),
                Arg.Any<CancellationToken>());
        }
        await mariaDbHandlerMock.DidNotReceive()
            .UpdateGistAsync(Arg.Any<Gist>(), Arg.Any<TransactionHandle>(), Arg.Any<CancellationToken>());
        await mariaDbHandlerMock.DidNotReceive().UpdateSummaryAsync(Arg.Any<Summary>(), Arg.Any<TransactionHandle>(),
            Arg.Any<CancellationToken>());
    }

    private static IMariaDbHandler CreateMariaDbHandlerMock(List<TestFeedData> testFeeds)
    {
        var mariaDbHandlerMock = Substitute.For<IMariaDbHandler>();
        var handle = CreateMockedTransactionHandle();
        mariaDbHandlerMock.OpenTransactionAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(handle));
        foreach (var gist in testFeeds.SelectMany(feed => feed.Gists))
        {
            mariaDbHandlerMock.InsertGistAsync(gist with { Id = null }, handle, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(gist.Id!.Value));
        }
        return mariaDbHandlerMock;
    }

    private static TransactionHandle CreateMockedTransactionHandle()
    {
        var connectionMock = Substitute.For<System.Data.Common.DbConnection>();
        var transactionMock = Substitute.For<System.Data.Common.DbTransaction>();
        return new TransactionHandle(connectionMock, transactionMock);
    }

    private static IWebCrawlHandler CreateMockedRssEntryHandler(List<TestFeedData> testFeeds)
    {
        var rssEntryHandlerMock = Substitute.For<IWebCrawlHandler>();
        foreach (var (entry, text) in testFeeds.SelectMany(feed => feed.Entries.Zip(feed.Texts)))
        {
            rssEntryHandlerMock.FetchPageContentAsync(entry.Url.AbsoluteUri).Returns(Task.FromResult(text));
        }
        return rssEntryHandlerMock;
    }

    private static IOpenAIHandler CreateMockedOpenAIHandler(List<TestFeedData> testFeeds)
    {
        var openAIHandlerMock = Substitute.For<IOpenAIHandler>();
        foreach (var feed in testFeeds)
        {
            foreach (var (entry, text, aiResponse) in feed.Entries.Zip(feed.Texts, feed.SummaryAIResponses))
            {
                openAIHandlerMock
                    .GenerateSummaryAIResponseAsync(feed.RssFeed.Language, entry.Title, text,
                        Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult(aiResponse));
            }
        }
        return openAIHandlerMock;
    }

    private static GistService CreateGistService(
        List<TestFeedData> testFeeds,
        IWebCrawlHandler? webCrawlHandler = null,
        IMariaDbHandler? mariaDbHandlerMock = null,
        IOpenAIHandler? openAIHandlerMock = null,
        IChromaDbHandler? chromaDbHandlerMock = null,
        ILogger<GistService>? loggerMock = null
    ) =>
        new(
            CreateRssFeedHandler(CreateMockedHttpClient(testFeeds), testFeeds),
            webCrawlHandler ?? CreateMockedRssEntryHandler(testFeeds),
            mariaDbHandlerMock ?? Substitute.For<IMariaDbHandler>(),
            openAIHandlerMock ?? CreateMockedOpenAIHandler(testFeeds),
            chromaDbHandlerMock ?? Substitute.For<IChromaDbHandler>(),
            loggerMock
        );
}
