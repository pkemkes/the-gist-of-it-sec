using System.Net;
using GistBackend.Exceptions;
using GistBackend.Handlers;
using GistBackend.Handlers.ChromaDbHandler;
using GistBackend.Handlers.MariaDbHandler;
using GistBackend.Services;
using GistBackend.Types;
using GistBackend.Utils;
using Microsoft.Extensions.Options;
using NSubstitute;
using static TestUtilities.TestData;
using IHttpClientFactory = System.Net.Http.IHttpClientFactory;

namespace GistBackend.UnitTest;

public class CleanupServiceTests
{
    [Fact]
    public async Task StartAsync_NoGistsInFeeds_NothingCleanedUp()
    {
        var testRssFeed = CreateTestRssFeed();
        var mariaDbHandlerMock = CreateDefaultMariaDbHandlerMock([], [testRssFeed]);
        mariaDbHandlerMock.GetAllGistsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<List<Gist>>([]));
        var chromaDbHandlerMock = CreateDefaultChromaDbHandlerMock();
        var gistDebouncerMock = Substitute.For<IGistDebouncer>();
        var service = CreateService(
            mariaDbHandlerMock: mariaDbHandlerMock,
            chromaDbHandlerMock: chromaDbHandlerMock,
            gistDebouncerMock: gistDebouncerMock,
            testRssFeeds: [testRssFeed],
            testGists: []);

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        await mariaDbHandlerMock
            .DidNotReceive()
            .EnsureCorrectDisabledStateForGistAsync(Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
        await chromaDbHandlerMock
            .DidNotReceive()
            .EnsureGistHasCorrectMetadataAsync(Arg.Any<Gist>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
        gistDebouncerMock.DidNotReceive().IsDebounced(Arg.Any<int>());
    }

    [Fact]
    public async Task StartAsync_FeedUrlNotPresentInDb_UnexpectedStateException()
    {
        var testRssFeed = CreateTestRssFeed();
        var rssFeedHandlerMock = Substitute.For<IRssFeedHandler>();
        rssFeedHandlerMock.Definitions.Returns([testRssFeed]);
        var mariaDbHandlerMock = CreateDefaultMariaDbHandlerMock(testRssFeeds: [testRssFeed]);
        mariaDbHandlerMock
            .GetFeedInfoByRssUrlAsync(testRssFeed.RssUrl,
                Arg.Any<CancellationToken>()).Returns(Task.FromResult<RssFeedInfo?>(null));
        var service = CreateService(rssFeedHandlerMock: rssFeedHandlerMock, mariaDbHandlerMock: mariaDbHandlerMock);

        await Assert.ThrowsAsync<UnexpectedStateException>(() => service.StartAsync(CancellationToken.None));
    }

    [Fact]
    public async Task StartAsync_AllGistsDebounced_NothingCleanedUp()
    {
        var testRssFeed = CreateTestRssFeed();
        var testGists = CreateDefaultTestGists(testRssFeed);
        var mariaDbHandlerMock = CreateDefaultMariaDbHandlerMock(testGists, [testRssFeed]);
        var chromaDbHandlerMock = CreateDefaultChromaDbHandlerMock();
        var gistDebouncerMock = Substitute.For<IGistDebouncer>();
        gistDebouncerMock.IsDebounced(Arg.Any<int>()).Returns(true);
        var service = CreateService(
            mariaDbHandlerMock: mariaDbHandlerMock,
            chromaDbHandlerMock: chromaDbHandlerMock,
            gistDebouncerMock: gistDebouncerMock,
            testRssFeeds: [testRssFeed],
            testGists: testGists);

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
    public async Task StartAsync_AllGistsAreFromDomainsToIgnore_AllGistsEnsuredToBeEnabled()
    {
        var testRssFeed = CreateTestRssFeed();
        var testGists = CreateDefaultTestGists(testRssFeed);
        var mariaDbHandlerMock = CreateDefaultMariaDbHandlerMock(testGists, [testRssFeed]);
        var chromaDbHandlerMock = CreateDefaultChromaDbHandlerMock();
        var options = Options.Create(new CleanupServiceOptions
        {
            DomainsToIgnore = testGists.Select(gist => gist.Url.Split(" ").First()).ToArray()
        });
        var service = CreateService(
            mariaDbHandlerMock: mariaDbHandlerMock,
            chromaDbHandlerMock: chromaDbHandlerMock,
            options: options,
            testGists: testGists,
            testRssFeeds: [testRssFeed]);

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        foreach (var gist in testGists)
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
        var testRssFeed = CreateTestRssFeed();
        var testGists = CreateDefaultTestGists(testRssFeed);
        var mariaDbHandlerMock = CreateDefaultMariaDbHandlerMock(testGists, [testRssFeed]);
        var chromaDbHandlerMock = CreateDefaultChromaDbHandlerMock();
        var httpClientFactoryMock = CreateHttpClientFactoryMock(statusCode);
        var service = CreateService(
            mariaDbHandlerMock: mariaDbHandlerMock,
            chromaDbHandlerMock: chromaDbHandlerMock,
            httpClientFactoryMock: httpClientFactoryMock,
            testGists: testGists,
            testRssFeeds: [testRssFeed]);

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        foreach (var gist in testGists)
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
    public async Task StartAsync_GistShouldBeDisabledBecauseOfRedirect_GistEnsuredToBeDisabled()
    {
        var testRssFeed = CreateTestRssFeed();
        var testGist = CreateTestGist(testRssFeed.Id);
        var mariaDbHandlerMock = CreateDefaultMariaDbHandlerMock([testGist], [testRssFeed]);
        mariaDbHandlerMock.GetAllGistsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<Gist> { testGist }));
        var chromaDbHandlerMock = CreateDefaultChromaDbHandlerMock();
        var httpClientFactoryMock = CreateHttpClientFactoryMock(HttpStatusCode.OK, "another different url");
        var service = CreateService(
            mariaDbHandlerMock: mariaDbHandlerMock,
            chromaDbHandlerMock: chromaDbHandlerMock,
            httpClientFactoryMock: httpClientFactoryMock,
            testGists: [testGist],
            testRssFeeds: [testRssFeed]);

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        await mariaDbHandlerMock
            .Received()
            .EnsureCorrectDisabledStateForGistAsync(testGist.Id!.Value, true, Arg.Any<CancellationToken>());
        await chromaDbHandlerMock
            .Received()
            .EnsureGistHasCorrectMetadataAsync(testGist, true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_GistsDisabledStateWereAlreadyCorrect_DebounceStateNotReset()
    {
        var testRssFeed = CreateTestRssFeed();
        var testGists = CreateDefaultTestGists(testRssFeed);
        var mariaDbHandlerMock = CreateDefaultMariaDbHandlerMock(testGists, [testRssFeed]);
        var chromaDbHandlerMock = CreateDefaultChromaDbHandlerMock();
        var gistDebouncerMock = Substitute.For<IGistDebouncer>();
        var service = CreateService(
            mariaDbHandlerMock: mariaDbHandlerMock,
            chromaDbHandlerMock: chromaDbHandlerMock,
            gistDebouncerMock: gistDebouncerMock,
            testGists: testGists,
            testRssFeeds: [testRssFeed]);

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        gistDebouncerMock.DidNotReceive().ResetDebounceState(Arg.Any<int>());
    }

    [Fact]
    public async Task StartAsync_GistsDisabledStateWereNotAlreadyCorrectInDb_DebounceStateReset()
    {
        var testRssFeed = CreateTestRssFeed();
        var testGists = CreateDefaultTestGists(testRssFeed);
        var mariaDbHandlerMock = CreateDefaultMariaDbHandlerMock(testGists, [testRssFeed]);
        mariaDbHandlerMock
            .EnsureCorrectDisabledStateForGistAsync(Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));
        var chromaDbHandlerMock = CreateDefaultChromaDbHandlerMock();
        var gistDebouncerMock = Substitute.For<IGistDebouncer>();
        var service = CreateService(
            mariaDbHandlerMock: mariaDbHandlerMock,
            chromaDbHandlerMock: chromaDbHandlerMock,
            gistDebouncerMock: gistDebouncerMock,
            testGists: testGists,
            testRssFeeds: [testRssFeed]);

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        testGists.ForEach(gist => gistDebouncerMock.Received().ResetDebounceState(gist.Id!.Value));
    }

    [Fact]
    public async Task StartAsync_GistsDisabledStateWereNotAlreadyCorrectInChromaDb_DebounceStateReset()
    {
        var testRssFeed = CreateTestRssFeed();
        var testGists = CreateDefaultTestGists(testRssFeed);
        var mariaDbHandlerMock = CreateDefaultMariaDbHandlerMock(testGists, [testRssFeed]);
        var chromaDbHandlerMock = CreateDefaultChromaDbHandlerMock();
        chromaDbHandlerMock
            .EnsureGistHasCorrectMetadataAsync(Arg.Any<Gist>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(false);
        var gistDebouncerMock = Substitute.For<IGistDebouncer>();
        var service = CreateService(
            mariaDbHandlerMock: mariaDbHandlerMock,
            chromaDbHandlerMock: chromaDbHandlerMock,
            gistDebouncerMock: gistDebouncerMock,
            testGists: testGists,
            testRssFeeds: [testRssFeed]);

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        testGists.ForEach(gist => gistDebouncerMock.Received().ResetDebounceState(gist.Id!.Value));
    }

    private static List<Gist> CreateDefaultTestGists(RssFeed feed)
    {
        var gists = CreateTestGists(5, feed.Id);
        var entries = gists.Select(gist => CreateTestEntry() with { Url = gist.Url }).ToList();
        feed.Entries = entries;
        return gists;
    }

    private static CleanupService CreateService(
        IRssFeedHandler? rssFeedHandlerMock = null,
        IGistDebouncer? gistDebouncerMock = null,
        IMariaDbHandler? mariaDbHandlerMock = null,
        IChromaDbHandler? chromaDbHandlerMock = null,
        IHttpClientFactory? httpClientFactoryMock = null,
        IOptions<CleanupServiceOptions>? options = null,
        List<RssFeed>? testRssFeeds = null,
        List<Gist>? testGists = null)
    {
        return new CleanupService(
            rssFeedHandlerMock ?? CreateDefaultRssFeedHandlerMock(testRssFeeds),
            gistDebouncerMock ?? Substitute.For<IGistDebouncer>(),
            mariaDbHandlerMock ?? CreateDefaultMariaDbHandlerMock(testGists, testRssFeeds),
            chromaDbHandlerMock ?? CreateDefaultChromaDbHandlerMock(),
            httpClientFactoryMock ?? CreateHttpClientFactoryMock(HttpStatusCode.OK),
            options ?? Options.Create(new CleanupServiceOptions { DomainsToIgnore = [] }),
            null
        );
    }

    private static IRssFeedHandler CreateDefaultRssFeedHandlerMock(List<RssFeed>? testRssFeeds = null)
    {
        var rssFeedHandlerMock = Substitute.For<IRssFeedHandler>();
        rssFeedHandlerMock.Definitions.Returns(testRssFeeds ?? CreateTestRssFeeds(5));
        return rssFeedHandlerMock;
    }

    private static IMariaDbHandler CreateDefaultMariaDbHandlerMock(List<Gist>? testGists = null,
        List<RssFeed>? testRssFeeds = null)
    {
        testRssFeeds ??= CreateTestRssFeeds(5);
        var mariaDbHandlerMock = Substitute.For<IMariaDbHandler>();
        mariaDbHandlerMock
            .GetAllGistsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(testGists ?? CreateTestGists(5, testRssFeeds.First().Id)));
        testRssFeeds.ForEach(feed =>
            mariaDbHandlerMock.GetFeedInfoByRssUrlAsync(feed.RssUrl, Arg.Any<CancellationToken>())
                .Returns(feed.ToRssFeedInfo()));
        mariaDbHandlerMock.EnsureCorrectDisabledStateForGistAsync(Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
        return mariaDbHandlerMock;
    }

    private static IChromaDbHandler CreateDefaultChromaDbHandlerMock()
    {
        var chromaDbHandlerMock = Substitute.For<IChromaDbHandler>();
        chromaDbHandlerMock.EnsureGistHasCorrectMetadataAsync(Arg.Any<Gist>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
        return chromaDbHandlerMock;
    }

    private static IHttpClientFactory CreateHttpClientFactoryMock(HttpStatusCode returnedStatusCode, string? urlInResponse = null)
    {
        var httpClientFactoryMock = Substitute.For<IHttpClientFactory>();
        var httpClientMock = Substitute.For<HttpClient>();
        var response = new HttpResponseMessage { StatusCode = returnedStatusCode };
        if (urlInResponse is not null)
        {
            response.RequestMessage = new HttpRequestMessage(HttpMethod.Get, urlInResponse);
        }
        httpClientMock.SendAsync(Arg.Any<HttpRequestMessage>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(response));
        httpClientFactoryMock.CreateClient(StartUp.RetryingHttpClientName)
            .Returns(httpClientMock);
        return httpClientFactoryMock;
    }
}
