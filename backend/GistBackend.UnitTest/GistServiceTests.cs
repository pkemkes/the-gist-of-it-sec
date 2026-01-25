using GistBackend.Handlers.AIHandler;
using GistBackend.Handlers.ChromaDbHandler;
using GistBackend.Handlers.MariaDbHandler;
using GistBackend.Handlers.WebCrawlHandler;
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
        var gistService = CreateGistService(mariaDbHandlerMock: mariaDbHandlerMock, testFeedDatas: testFeeds);

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
        var oldFeedData = new TestFeedData {
            RssFeed = {
                Id = 0
            }
        };
        var newFeedData = new TestFeedData {
            RssFeed = {
                Id = oldFeedData.RssFeed.Id
            }
        };
        var testFeedDatas = new List<TestFeedData> { oldFeedData, newFeedData };
        var mariaDbHandlerMock = CreateMariaDbHandlerMock(testFeedDatas);
        mariaDbHandlerMock
            .GetFeedInfoByRssUrlAsync(newFeedData.RssFeed.RssUrl, Arg.Any<CancellationToken>())!
            .Returns(Task.FromResult(oldFeedData.RssFeedInfo));
        var gistService = CreateGistService(
            testFeedDatas: testFeedDatas,
            mariaDbHandlerMock: mariaDbHandlerMock
        );

        await gistService.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        await mariaDbHandlerMock.Received(1).UpdateFeedInfoAsync(newFeedData.RssFeedInfo, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_TwoTestEntriesInFeed_EntriesProcessedFromOldestToNewest()
    {
        var testFeedData = new TestFeedData(CreateTestEntries(5).OrderByDescending(entry => entry.Updated).ToList());
        var mariaDbHandlerMock = CreateMariaDbHandlerMock([testFeedData]);
        var gistService = CreateGistService(mariaDbHandlerMock: mariaDbHandlerMock, testFeedDatas: [ testFeedData ]);

        await gistService.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        var orderedEntries = testFeedData.Entries.OrderBy(entry => entry.Updated).ToArray();
        Received.InOrder(async () => {
            foreach (var entry in orderedEntries)
                await mariaDbHandlerMock.GetGistByReferenceAsync(entry.Reference, Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task StartAsync_OldVersionOfGistsExist_GistsAreGeneratedAndUpdated()
    {
        var testFeedData = new TestFeedData(feedId: 0);
        var mariaDbHandlerMock = CreateMariaDbHandlerMock([testFeedData]);
        testFeedData.Gists.ForEach(gist =>
            mariaDbHandlerMock.GetGistByReferenceAsync(gist.Reference, Arg.Any<CancellationToken>())!
                .Returns(Task.FromResult(gist with { Updated = gist.Updated.AddDays(-5)}))
        );
        var chromaDbHandlerMock = Substitute.For<IChromaDbHandler>();
        var gistService = CreateGistService(
            testFeedDatas: [testFeedData],
            mariaDbHandlerMock: mariaDbHandlerMock,
            chromaDbHandlerMock: chromaDbHandlerMock
        );

        await gistService.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        foreach (var (entry, summaryAIResponse) in testFeedData.Entries.Zip(testFeedData.SummaryAIResponses))
        {
            await chromaDbHandlerMock.Received(1).UpsertEntryAsync(
                Arg.Is<RssEntry>(e => e.Reference == entry.Reference && e.FeedId == entry.FeedId),
                Arg.Any<string>(), Arg.Any<CancellationToken>());
            var gistId = testFeedData.Gists.First(gist => gist.Reference == entry.Reference).Id!.Value;
            var feedLanguage = testFeedData.RssFeed.Language;
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
        var testFeedData = new TestFeedData(feedId: 0);
        var mariaDbHandlerMock = CreateMariaDbHandlerMock([testFeedData]);
        var chromaDbHandlerMock = Substitute.For<IChromaDbHandler>();
        var gistService = CreateGistService(
            mariaDbHandlerMock: mariaDbHandlerMock,
            chromaDbHandlerMock: chromaDbHandlerMock,
            testFeedDatas: [testFeedData]
        );

        await gistService.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        foreach (var (entry, summaryAIResponse, gist) in testFeedData.Entries.Zip(testFeedData.SummaryAIResponses,
                     testFeedData.Gists))
        {
            var feedLanguage = testFeedData.RssFeed.Language;
            await chromaDbHandlerMock.Received(1)
                .UpsertEntryAsync(Arg.Is<RssEntry>(e => e.Reference == entry.Reference && e.FeedId == entry.FeedId),
                    summaryAIResponse.SummaryEnglish, Arg.Any<CancellationToken>());
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

    [Fact]
    public async Task StartAsync_FeedHasSomeSponsoredEntries_SponsoredStateCorrectlyInserted()
    {
        const int feedId = 0;
        var texts = new List<string>
        {
            "Normal content",
            "Another normal content",
            $"Content with a {TestFeed.SponsoredContentMarker}",
            "More normal content",
            $"Sponsored content here {TestFeed.SponsoredContentMarker}"
        };
        var entries = CreateTestEntries(texts.Count, feedId);
        var summaryAIResponses = CreateTestSummaryAIResponses(entries.Count);
        var gists = entries.Zip(summaryAIResponses, CreateTestGistFromEntry).ToList();
        for (var i = 0; i < entries.Count; i++)
        {
            if (texts[i].Contains(TestFeed.SponsoredContentMarker))
                gists[i] = gists[i] with { IsSponsoredContent = true };
        }
        var testFeedData = new TestFeedData(entries, summaryAIResponses, texts, gists, feedId);
        var mariaDbHandlerMock = CreateMariaDbHandlerMock([testFeedData]);
        var gistService = CreateGistService(
            mariaDbHandlerMock: mariaDbHandlerMock,
            testFeedDatas: [testFeedData]
        );

        await gistService.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        foreach (var (entry, summaryAIResponse, gist) in testFeedData.Entries.Zip(testFeedData.SummaryAIResponses,
                     testFeedData.Gists))
        {
            var feedLanguage = testFeedData.RssFeed.Language;
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

    private static IWebCrawlHandler CreateMockedWebCrawlHandler(List<TestFeedData> testFeeds)
    {
        var webCrawlHandlerMock = Substitute.For<IWebCrawlHandler>();
        foreach (var (entry, text) in testFeeds.SelectMany(feed => feed.Entries.Zip(feed.Texts)))
        {
            var response = new FetchResponse(200, text, false);
            webCrawlHandlerMock.FetchAsync(entry.Url.AbsoluteUri, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(response));
        }
        return webCrawlHandlerMock;
    }

    private static IAIHandler CreateMockedAIHandler(List<TestFeedData> testFeeds)
    {
        var aiHandlerMock = Substitute.For<IAIHandler>();
        foreach (var feed in testFeeds)
        {
            foreach (var (entry, text, aiResponse) in feed.Entries.Zip(feed.Texts, feed.SummaryAIResponses))
            {
                aiHandlerMock
                    .GenerateSummaryAIResponseAsync(feed.RssFeed.Language, entry.Title, text,
                        Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult(aiResponse));
            }
        }
        return aiHandlerMock;
    }

    private static GistService CreateGistService(
        List<TestFeedData> testFeedDatas,
        IWebCrawlHandler? webCrawlHandler = null,
        IMariaDbHandler? mariaDbHandlerMock = null,
        IAIHandler? aiHandlerMock = null,
        IChromaDbHandler? chromaDbHandlerMock = null,
        ILogger<GistService>? loggerMock = null
    ) =>
        new(
            CreateRssFeedHandler(CreateMockedHttpClient(testFeedDatas), testFeedDatas),
            webCrawlHandler ?? CreateMockedWebCrawlHandler(testFeedDatas),
            mariaDbHandlerMock ?? Substitute.For<IMariaDbHandler>(),
            aiHandlerMock ?? CreateMockedAIHandler(testFeedDatas),
            chromaDbHandlerMock ?? Substitute.For<IChromaDbHandler>(),
            loggerMock
        );
}
