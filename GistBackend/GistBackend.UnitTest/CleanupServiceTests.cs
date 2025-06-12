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
using static GistBackend.UnitTest.Utils.TestData;
using IHttpClientFactory = System.Net.Http.IHttpClientFactory;

namespace GistBackend.UnitTest;

public class CleanupServiceTests
{
    [Fact]
    public async Task StartAsync_NoGistsInFeeds_NothingCleanedUp()
    {
        var mariaDbHandlerMock = CreateDefaultMariaDbHandlerMock();
        mariaDbHandlerMock.GetAllGistsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<List<Gist>>([]));
        var chromaDbHandlerMock = CreateDefaultChromaDbHandlerMock();
        var gistDebouncerMock = Substitute.For<IGistDebouncer>();
        var service = CreateService(
            mariaDbHandlerMock: mariaDbHandlerMock,
            chromaDbHandlerMock: chromaDbHandlerMock,
            gistDebouncerMock: gistDebouncerMock);

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
        var testRssFeed = TestRssFeeds.First();
        var rssFeedHandlerMock = Substitute.For<IRssFeedHandler>();
        rssFeedHandlerMock.Definitions.Returns([testRssFeed]);
        var mariaDbHandlerMock = CreateDefaultMariaDbHandlerMock();
        mariaDbHandlerMock
            .GetFeedInfoByRssUrlAsync(testRssFeed.RssUrl,
                Arg.Any<CancellationToken>()).Returns(Task.FromResult<RssFeedInfo?>(null));
        var service = CreateService(rssFeedHandlerMock: rssFeedHandlerMock, mariaDbHandlerMock: mariaDbHandlerMock);

        await Assert.ThrowsAsync<UnexpectedStateException>(() => service.StartAsync(CancellationToken.None));
    }

    [Fact]
    public async Task StartAsync_AllGistsDebounced_NothingCleanedUp()
    {
        var mariaDbHandlerMock = CreateDefaultMariaDbHandlerMock();
        var chromaDbHandlerMock = CreateDefaultChromaDbHandlerMock();
        var gistDebouncerMock = Substitute.For<IGistDebouncer>();
        gistDebouncerMock.IsDebounced(Arg.Any<int>()).Returns(true);
        var service = CreateService(
            mariaDbHandlerMock: mariaDbHandlerMock,
            chromaDbHandlerMock: chromaDbHandlerMock,
            gistDebouncerMock: gistDebouncerMock);

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
        var mariaDbHandlerMock = CreateDefaultMariaDbHandlerMock();
        var chromaDbHandlerMock = CreateDefaultChromaDbHandlerMock();
        var options = Options.Create(new CleanupServiceOptions
        {
            DomainsToIgnore = TestRssEntries.Select(entry => entry.Url.Split(" ").First()).ToArray()
        });
        var service = CreateService(
            mariaDbHandlerMock: mariaDbHandlerMock,
            chromaDbHandlerMock: chromaDbHandlerMock,
            options: options);

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        foreach (var gist in TestGists)
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
        var mariaDbHandlerMock = CreateDefaultMariaDbHandlerMock();
        var chromaDbHandlerMock = CreateDefaultChromaDbHandlerMock();
        var httpClientFactoryMock = CreateHttpClientFactoryMock(statusCode);
        var service = CreateService(
            mariaDbHandlerMock: mariaDbHandlerMock,
            chromaDbHandlerMock: chromaDbHandlerMock,
            httpClientFactoryMock: httpClientFactoryMock);

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        foreach (var gist in TestGists)
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
        var testGist = TestGists.First() with { Url = "different url" };
        var mariaDbHandlerMock = CreateDefaultMariaDbHandlerMock();
        mariaDbHandlerMock.GetAllGistsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<Gist> { testGist }));
        var chromaDbHandlerMock = CreateDefaultChromaDbHandlerMock();
        var httpClientFactoryMock = CreateHttpClientFactoryMock(HttpStatusCode.OK, "another different url");
        var service = CreateService(
            mariaDbHandlerMock: mariaDbHandlerMock,
            chromaDbHandlerMock: chromaDbHandlerMock,
            httpClientFactoryMock: httpClientFactoryMock);

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
        var mariaDbHandlerMock = CreateDefaultMariaDbHandlerMock();
        var chromaDbHandlerMock = CreateDefaultChromaDbHandlerMock();
        var gistDebouncerMock = Substitute.For<IGistDebouncer>();
        var service = CreateService(
            mariaDbHandlerMock: mariaDbHandlerMock,
            chromaDbHandlerMock: chromaDbHandlerMock,
            gistDebouncerMock: gistDebouncerMock);

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        gistDebouncerMock.DidNotReceive().ResetDebounceState(Arg.Any<int>());
    }

    [Fact]
    public async Task StartAsync_GistsDisabledStateWereNotAlreadyCorrectInDb_DebounceStateReset()
    {
        var mariaDbHandlerMock = CreateDefaultMariaDbHandlerMock();
        mariaDbHandlerMock
            .EnsureCorrectDisabledStateForGistAsync(Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));
        var chromaDbHandlerMock = CreateDefaultChromaDbHandlerMock();
        var gistDebouncerMock = Substitute.For<IGistDebouncer>();
        var service = CreateService(
            mariaDbHandlerMock: mariaDbHandlerMock,
            chromaDbHandlerMock: chromaDbHandlerMock,
            gistDebouncerMock: gistDebouncerMock);

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        TestGists.ForEach(gist => gistDebouncerMock.Received().ResetDebounceState(gist.Id!.Value));
    }

    [Fact]
    public async Task StartAsync_GistsDisabledStateWereNotAlreadyCorrectInChromaDb_DebounceStateReset()
    {
        var mariaDbHandlerMock = CreateDefaultMariaDbHandlerMock();
        var chromaDbHandlerMock = CreateDefaultChromaDbHandlerMock();
        chromaDbHandlerMock
            .EnsureGistHasCorrectMetadataAsync(Arg.Any<Gist>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(false);
        var gistDebouncerMock = Substitute.For<IGistDebouncer>();
        var service = CreateService(
            mariaDbHandlerMock: mariaDbHandlerMock,
            chromaDbHandlerMock: chromaDbHandlerMock,
            gistDebouncerMock: gistDebouncerMock);

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));

        TestGists.ForEach(gist => gistDebouncerMock.Received().ResetDebounceState(gist.Id!.Value));
    }

    private static CleanupService CreateService(
        IRssFeedHandler? rssFeedHandlerMock = null,
        IGistDebouncer? gistDebouncerMock = null,
        IMariaDbHandler? mariaDbHandlerMock = null,
        IChromaDbHandler? chromaDbHandlerMock = null,
        IHttpClientFactory? httpClientFactoryMock = null,
        IOptions<CleanupServiceOptions>? options = null)
    {
        return new CleanupService(
            rssFeedHandlerMock ?? CreateDefaultRssFeedHandlerMock(),
            gistDebouncerMock ?? Substitute.For<IGistDebouncer>(),
            mariaDbHandlerMock ?? CreateDefaultMariaDbHandlerMock(),
            chromaDbHandlerMock ?? CreateDefaultChromaDbHandlerMock(),
            httpClientFactoryMock ?? CreateHttpClientFactoryMock(HttpStatusCode.OK),
            options ?? Options.Create(new CleanupServiceOptions { DomainsToIgnore = [] }),
            null
        );
    }

    private static IRssFeedHandler CreateDefaultRssFeedHandlerMock()
    {
        var rssFeedHandlerMock = Substitute.For<IRssFeedHandler>();
        rssFeedHandlerMock.Definitions.Returns(TestRssFeeds);
        return rssFeedHandlerMock;
    }

    private static IMariaDbHandler CreateDefaultMariaDbHandlerMock()
    {
        var mariaDbHandlerMock = Substitute.For<IMariaDbHandler>();
        mariaDbHandlerMock
            .GetAllGistsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TestGists));
        TestRssFeeds.ForEach(feed =>
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
        httpClientFactoryMock.CreateClient(Program.RetryingHttpClientName)
            .Returns(httpClientMock);
        return httpClientFactoryMock;
    }
}
