using System.Net;
using GistBackend.Exceptions;
using GistBackend.Handler.GoogleSearchHandler;
using GistBackend.Types;
using GistBackend.UnitTest.Utils;
using Google;
using Google.Apis.CustomSearchAPI.v1.Data;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using static GistBackend.Utils.LogEvents;

namespace GistBackend.UnitTest;

public class GoogleSearchHandlerTests
{
    private const string TestSearchQuery = "test search query";

    private readonly Search _testSearch = new() {
        Items = [
            new Result {
                Title = "first test title",
                Snippet = "first test snippet",
                Link = "first test link",
                DisplayLink = "first test display link",
                Image = new Result.ImageData {
                    ThumbnailLink = "first test thumbnail link"
                }
            },
            new Result {
                Title = "second test title",
                Snippet = "second test snippet",
                Link = "second test link",
                DisplayLink = "second test display link",
                Image = new Result.ImageData {
                    ThumbnailLink = "second test thumbnail link"
                }
            }
        ]
    };

    private static GoogleSearchResult[] GenerateTestGoogleSearchResults(int gistId) => [
        new(
            gistId,
            "first test title",
            "first test snippet",
            "first test link",
            "first test display link",
            "first test thumbnail link"
        ),
        new(
            gistId,
            "second test title",
            "second test snippet",
            "second test link",
            "second test display link",
            "second test thumbnail link"
        )
    ];

    [Fact]
    public async Task ExecuteSearchAsync_ValidQuery_ReturnsSearchResults()
    {
        var customSearchApiHandlerMock = Substitute.For<ICustomSearchApiHandler>();
        customSearchApiHandlerMock.ExecuteSearchAsync(TestSearchQuery, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Search?>(_testSearch));
        var handler = new GoogleSearchHandler(customSearchApiHandlerMock, null);
        const int gistId = 1337;
        var expectedTestGoogleSearchResults = GenerateTestGoogleSearchResults(gistId);

        var actual = await handler.GetSearchResultsAsync(TestSearchQuery, gistId, CancellationToken.None);

        Assert.NotNull(actual);
        Assert.Equal(2, actual.Count);
        Assert.Equivalent(expectedTestGoogleSearchResults, actual);
    }

    [Fact]
    public async Task ExecuteSearchAsync_QuotaExceeded_ReturnsNullAndCorrectLogEventLogged()
    {
        var customSearchApiHandlerMock = Substitute.For<ICustomSearchApiHandler>();
        var exception = new GoogleApiException("test service") { HttpStatusCode = HttpStatusCode.TooManyRequests };
        customSearchApiHandlerMock.ExecuteSearchAsync(TestSearchQuery, Arg.Any<CancellationToken>())
            .ThrowsAsync(exception);
        var loggerMock = Substitute.For<ILogger<GoogleSearchHandler>>();
        var logAsserter = new LogAsserter(loggerMock);
        var handler = new GoogleSearchHandler(customSearchApiHandlerMock, loggerMock);

        var actual = await handler.GetSearchResultsAsync(TestSearchQuery, 1337, CancellationToken.None);

        Assert.Null(actual);
        logAsserter.AssertCorrectErrorLog(GoogleApiQuotaExceeded, exception);
    }

    [Fact]
    public async Task ExecuteSearchAsync_InternalServerError_ThrowsAndCorrectLogEventLogged()
    {
        var customSearchApiHandlerMock = Substitute.For<ICustomSearchApiHandler>();
        var exception = new GoogleApiException("test service") { HttpStatusCode = HttpStatusCode.InternalServerError };
        customSearchApiHandlerMock.ExecuteSearchAsync(TestSearchQuery, Arg.Any<CancellationToken>())
            .ThrowsAsync(exception);
        var loggerMock = Substitute.For<ILogger<GoogleSearchHandler>>();
        var logAsserter = new LogAsserter(loggerMock);
        var handler = new GoogleSearchHandler(customSearchApiHandlerMock, loggerMock);

        await Assert.ThrowsAsync<ExternalServiceException>(() =>
            handler.GetSearchResultsAsync(TestSearchQuery, 1337, CancellationToken.None));

        logAsserter.AssertCorrectErrorLog(UnexpectedGoogleApiException);
    }
}
