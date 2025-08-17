using System.Net;
using GistBackend.Exceptions;
using GistBackend.Handlers.GoogleSearchHandler;
using GistBackend.Types;
using GistBackend.UnitTest.Utils;
using Google;
using Google.Apis.CustomSearchAPI.v1.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
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
                Link = "https://first.test.link.com/",
                DisplayLink = "first.test.display.link.com",
                Pagemap = new Dictionary<string, object> {
                    {
                        "cse_thumbnail",
                        new JArray([
                            new JObject {{"src", "https://first.test.thumbnail.link.com/"}}
                        ])
                    }
                }
            },
            new Result {
                Title = "second test title",
                Snippet = "second test snippet",
                Link = "https://second.test.link.com/",
                DisplayLink = "second.test.display.link.com",
                Pagemap = new Dictionary<string, object> {
                    {
                        "cse_thumbnail",
                        new JArray([
                            new JObject {{"src", "https://second.test.thumbnail.link.com/"}}
                        ])
                    }
                }
            }
        ]
    };

    private static GoogleSearchResult[] GenerateTestGoogleSearchResults(int gistId) => [
        new(
            gistId,
            "first test title",
            "first test snippet",
            new Uri("https://first.test.link.com/"),
            "first.test.display.link.com",
            new Uri("https://first.test.thumbnail.link.com/")
        ),
        new(
            gistId,
            "second test title",
            "second test snippet",
            new Uri("https://second.test.link.com/"),
            "second.test.display.link.com",
            new Uri("https://second.test.thumbnail.link.com/")
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
