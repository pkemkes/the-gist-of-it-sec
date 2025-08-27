using System.Net;
using GistBackend.Exceptions;
using GistBackend.Handlers;
using GistBackend.Handlers.ChromaDbHandler;
using GistBackend.Handlers.MariaDbHandler;
using GistBackend.Services;
using GistBackend.Types;
using GistBackend.Utils;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using NSubstitute;
using TestUtilities;
using static TestUtilities.TestData;

namespace GistBackend.UnitTest;

public class CleanupServiceTests
{
     [Fact]
     public async Task StartAsync_NoGistsInFeeds_NothingCleanedUp()
     {
         var testFeed = new TestFeedData([], 0);
         var mariaDbHandlerMock = CreateDefaultMariaDbHandlerMock([testFeed]);
         var chromaDbHandlerMock = CreateDefaultChromaDbHandlerMock();
         var gistDebouncerMock = Substitute.For<IGistDebouncer>();
         var service = CreateCleanupService(
             [testFeed],
             mariaDbHandlerMock,
             chromaDbHandlerMock,
             gistDebouncerMock);

         await service.StartAsync(CancellationToken.None);
         await Task.Delay(TimeSpan.FromSeconds(2));

         await mariaDbHandlerMock
             .DidNotReceive()
             .EnsureCorrectDisabledStateForGistAsync(Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
         await chromaDbHandlerMock
             .DidNotReceive()
             .EnsureGistHasCorrectMetadataAsync(Arg.Any<Gist>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
         gistDebouncerMock.DidNotReceive().IsReady(Arg.Any<int>(), Arg.Any<DateTime>());
     }

     [Fact]
     public async Task StartAsync_AllGistsDebounced_NothingCleanedUp()
     {
         var testFeed = new TestFeedData(feedId: 0);
         var mariaDbHandlerMock = CreateDefaultMariaDbHandlerMock([testFeed]);
         var chromaDbHandlerMock = CreateDefaultChromaDbHandlerMock();
         var gistDebouncerMock = Substitute.For<IGistDebouncer>();
         gistDebouncerMock.IsReady(Arg.Any<int>(), Arg.Any<DateTime>()).Returns(false);
         var service = CreateCleanupService(
             [testFeed],
             mariaDbHandlerMock,
             chromaDbHandlerMock,
            gistDebouncerMock);

         await service.StartAsync(CancellationToken.None);
         await Task.Delay(TimeSpan.FromSeconds(2));

         await mariaDbHandlerMock
             .DidNotReceive()
             .EnsureCorrectDisabledStateForGistAsync(Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
         await chromaDbHandlerMock
             .DidNotReceive()
             .EnsureGistHasCorrectMetadataAsync(Arg.Any<Gist>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
     }

     [Fact]
     public async Task StartAsync_NotAllFeedsInDbYet_AllGistsAreProcessed()
     {
         var testFeedInDb = new TestFeedData(feedId: 0);
         var testFeedNotInDb = new TestFeedData(feedId: 1);
         var testFeeds = new List<TestFeedData>{ testFeedInDb, testFeedNotInDb };
         var mariaDbHandlerMock = CreateDefaultMariaDbHandlerMock([testFeedInDb]);
         var chromaDbHandlerMock = CreateDefaultChromaDbHandlerMock();
         var service = CreateCleanupService(
             testFeeds,
             mariaDbHandlerMock,
             chromaDbHandlerMock
         );

         await service.StartAsync(CancellationToken.None);
         await Task.Delay(TimeSpan.FromSeconds(2));

         foreach (var gist in testFeedInDb.Gists)
         {
             await mariaDbHandlerMock
                 .Received()
                 .EnsureCorrectDisabledStateForGistAsync(gist.Id!.Value, false, Arg.Any<CancellationToken>());
             await chromaDbHandlerMock
                 .Received()
                 .EnsureGistHasCorrectMetadataAsync(gist, false, Arg.Any<CancellationToken>());
         }
     }

     [Fact]
     public async Task StartAsync_OneFeedIsMissingInDb_GistsOfMissingFeedCauseException()
     {
         var testFeedInDb = new TestFeedData(feedId: 0);
         var testFeedNotInDb = new TestFeedData(feedId: 1);
         var testFeeds = new List<TestFeedData>{ testFeedInDb, testFeedNotInDb };
         var testGists = testFeeds.SelectMany(f => f.Gists).ToList();
         var mariaDbHandlerMock = CreateDefaultMariaDbHandlerMock([testFeedInDb], testGists);
         var service = CreateCleanupService(testFeeds, mariaDbHandlerMock);

         await Assert.ThrowsAsync<FeedNotFoundException>(async () => {
             await service.StartAsync(CancellationToken.None);
             await Task.Delay(TimeSpan.FromSeconds(2));
         });
     }

     [Fact]
     public async Task StartAsync_AllGistsAreFromDomainsToIgnore_AllGistsEnsuredToBeEnabled()
     {
         var testFeed = new TestFeedData(feedId: 0);
         var mariaDbHandlerMock = CreateDefaultMariaDbHandlerMock([testFeed]);
         var chromaDbHandlerMock = CreateDefaultChromaDbHandlerMock();
         var options = Options.Create(new CleanupServiceOptions
         {
             DomainsToIgnore = testFeed.Gists.Select(gist => gist.Url.Host).ToArray()
         });
         var service = CreateCleanupService(
             [testFeed],
             mariaDbHandlerMock,
             chromaDbHandlerMock,
             options: options);

         await service.StartAsync(CancellationToken.None);
         await Task.Delay(TimeSpan.FromSeconds(2));

         foreach (var gist in testFeed.Gists)
         {
             await mariaDbHandlerMock
                 .Received()
                 .EnsureCorrectDisabledStateForGistAsync(gist.Id!.Value, false, Arg.Any<CancellationToken>());
             await chromaDbHandlerMock
                 .Received()
                 .EnsureGistHasCorrectMetadataAsync(gist, false, Arg.Any<CancellationToken>());
         }
     }

     [Theory]
     [InlineData(HttpStatusCode.BadRequest, true)]
     [InlineData(HttpStatusCode.Unauthorized, true)]
     [InlineData(HttpStatusCode.NotFound, true)]
     [InlineData(HttpStatusCode.Gone, true)]
     [InlineData(HttpStatusCode.OK, false)]
     [InlineData(HttpStatusCode.Found, false)]
     [InlineData(HttpStatusCode.InternalServerError, false)]
     [InlineData(HttpStatusCode.BadGateway, false)]
     public async Task StartAsync_GistShouldBeDisabledBecauseOfStatusCode_CorrectDisabledStateEnsured(
         HttpStatusCode statusCode, bool disabled)
     {
         var testFeed = new TestFeedData(feedId: 0);
         var mariaDbHandlerMock = CreateDefaultMariaDbHandlerMock([testFeed]);
         var chromaDbHandlerMock = CreateDefaultChromaDbHandlerMock();
         var webCrawlHandlerMock = CreateWebCrawlHandlerMock((int)statusCode);
         var service = CreateCleanupService(
             [testFeed],
             mariaDbHandlerMock,
             chromaDbHandlerMock,
             webCrawlHandlerMock: webCrawlHandlerMock);

         await service.StartAsync(CancellationToken.None);
         await Task.Delay(TimeSpan.FromSeconds(2));

         foreach (var gist in testFeed.Gists)
         {
             await mariaDbHandlerMock
                 .Received()
                 .EnsureCorrectDisabledStateForGistAsync(gist.Id!.Value, disabled, Arg.Any<CancellationToken>());
             await chromaDbHandlerMock
                 .Received()
                 .EnsureGistHasCorrectMetadataAsync(gist, disabled, Arg.Any<CancellationToken>());
         }
     }

     [Fact]
     public async Task StartAsync_GistShouldBeDisabledBecauseOfRedirectAndNotPresentInFeed_GistEnsuredToBeDisabled()
     {
         const int feedId = 0;
         var testFeed = new TestFeedData(feedId: feedId);
         var redirectedEntry = CreateTestEntry(feedId);
         var redirectedGist = CreateTestGistFromEntry(redirectedEntry);
         var testGists = testFeed.Gists.Concat([redirectedGist]).ToList();
         var mariaDbHandlerMock = CreateDefaultMariaDbHandlerMock([testFeed], testGists);
         var chromaDbHandlerMock = CreateDefaultChromaDbHandlerMock();
         var webCrawlHandlerMock = CreateWebCrawlHandlerMock(200, "another different url");
         var service = CreateCleanupService(
             [testFeed],
             mariaDbHandlerMock,
             chromaDbHandlerMock,
             webCrawlHandlerMock: webCrawlHandlerMock);

         await service.StartAsync(CancellationToken.None);
         await Task.Delay(TimeSpan.FromSeconds(2));

         await mariaDbHandlerMock
             .Received()
             .EnsureCorrectDisabledStateForGistAsync(redirectedGist.Id!.Value, true, Arg.Any<CancellationToken>());
         await chromaDbHandlerMock
             .Received()
             .EnsureGistHasCorrectMetadataAsync(redirectedGist, true, Arg.Any<CancellationToken>());
     }


    private static CleanupService CreateCleanupService(
        List<TestFeedData> testFeeds,
        IMariaDbHandler? mariaDbHandlerMock = null,
        IChromaDbHandler? chromaDbHandlerMock = null,
        IGistDebouncer? gistDebouncerMock = null,
        IWebCrawlHandler? webCrawlHandlerMock = null,
        IOptions<CleanupServiceOptions>? options = null)
    {
        if (gistDebouncerMock is null)
        {
            gistDebouncerMock = Substitute.For<IGistDebouncer>();
            gistDebouncerMock.IsReady(Arg.Any<int>(), Arg.Any<DateTime>()).Returns(true);
        }
        return new CleanupService(
            CreateRssFeedHandler(CreateMockedHttpClient(testFeeds), testFeeds),
            gistDebouncerMock,
            mariaDbHandlerMock ?? CreateDefaultMariaDbHandlerMock(testFeeds),
            chromaDbHandlerMock ?? CreateDefaultChromaDbHandlerMock(),
            webCrawlHandlerMock ?? CreateWebCrawlHandlerMock(200),
            options ?? Options.Create(new CleanupServiceOptions { DomainsToIgnore = [] }),
            null
        );
    }

    private static IMariaDbHandler CreateDefaultMariaDbHandlerMock(List<TestFeedData> testFeeds,
        List<Gist>? testGists = null, bool ensureCorrectDisabledStateResult = true)
    {
        var mariaDbHandlerMock = Substitute.For<IMariaDbHandler>();
        mariaDbHandlerMock
            .GetAllGistsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(testGists ?? testFeeds.SelectMany(f => f.Gists).ToList()));
        testFeeds.ForEach(feed =>
            mariaDbHandlerMock.GetFeedInfoByRssUrlAsync(feed.RssFeedInfo.RssUrl, Arg.Any<CancellationToken>())
                .Returns(feed.RssFeedInfo));
        mariaDbHandlerMock.EnsureCorrectDisabledStateForGistAsync(Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ensureCorrectDisabledStateResult));
        return mariaDbHandlerMock;
    }

    private static IChromaDbHandler CreateDefaultChromaDbHandlerMock(bool ensureCorrectMetadataResult = true)
    {
        var chromaDbHandlerMock = Substitute.For<IChromaDbHandler>();
        chromaDbHandlerMock.EnsureGistHasCorrectMetadataAsync(Arg.Any<Gist>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ensureCorrectMetadataResult));
        return chromaDbHandlerMock;
    }

    private static IWebCrawlHandler CreateWebCrawlHandlerMock(int returnedStatusCode, string? urlInResponse = null)
    {
        var webCrawlHandlerMock = Substitute.For<IWebCrawlHandler>();
        var response = CreateFakePlaywrightResponse(returnedStatusCode, urlInResponse);
        webCrawlHandlerMock.FetchResponseAsync(Arg.Any<string>()).Returns(Task.FromResult(response));
        return webCrawlHandlerMock;
    }

    private static IResponse? CreateFakePlaywrightResponse(int statusCode, string? urlInResponse = null)
    {
        var responseMock = Substitute.For<IResponse?>();
        responseMock!.Status.Returns(statusCode);

        if (urlInResponse is null) return responseMock;

        var requestMock = Substitute.For<IRequest>();
        requestMock.Url.Returns(urlInResponse);
        responseMock.Request.Returns(requestMock);
        return responseMock;
    }
}
