using GistBackend.Exceptions;
using GistBackend.Handler;
using GistBackend.Handler.MariaDbHandler;
using GistBackend.IntegrationTest.Utils;
using GistBackend.Types;
using Microsoft.Extensions.Options;
using MySqlConnector;
using NSubstitute;

namespace GistBackend.IntegrationTest;

public class MariaDbHandlerTests(MariaDbFixture fixture) : IClassFixture<MariaDbFixture> {
    private readonly Random _random = new();

    private readonly MariaDbHandlerOptions _gistHandlerOptions = new (
        fixture.Hostname,
        MariaDbFixture.GistServiceDbUsername,
        MariaDbFixture.GistServiceDbPassword,
        fixture.ExposedPort
    );

    private readonly MariaDbHandlerOptions _recapHandlerOptions = new(
        fixture.Hostname,
        MariaDbFixture.RecapServiceDbUsername,
        MariaDbFixture.RecapServiceDbPassword,
        fixture.ExposedPort
    );

    private MariaDbAsserter GistAsserter => new(_gistHandlerOptions);
    private MariaDbAsserter RecapAsserter => new(_recapHandlerOptions);

    [Fact]
    public async Task InsertFeedInfoAsync_FeedInfoDoesNotExist_FeedInfoIsInsertedInDb()
    {
        var handler = CreateGistHandler();
        var feedInfoToInsert = CreateTestFeedInfo();

        await handler.InsertFeedInfoAsync(feedInfoToInsert, CancellationToken.None);

        await GistAsserter.AssertFeedInfoIsInDbAsync(feedInfoToInsert);
    }

    [Fact]
    public async Task InsertFeedInfoAsync_FeedInfoExistsAlready_ThrowsMySqlException()
    {
        var handler = CreateGistHandler();
        var feedInfoToInsert = CreateTestFeedInfo();
        await handler.InsertFeedInfoAsync(feedInfoToInsert, CancellationToken.None);

        await Assert.ThrowsAsync<MySqlException>(() =>
            handler.InsertFeedInfoAsync(feedInfoToInsert, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateFeedInfoAsync_DifferentTitle_TitleChanged()
    {
        var handler = CreateGistHandler();
        var feedInfoToUpdate = CreateTestFeedInfo();
        var feedInfoId = await handler.InsertFeedInfoAsync(feedInfoToUpdate, CancellationToken.None);
        var expectedFeedInfo = feedInfoToUpdate with { Title = "different title" };

        await handler.UpdateFeedInfoAsync(expectedFeedInfo, CancellationToken.None);

        await GistAsserter.AssertFeedInfoIsInDbAsync(expectedFeedInfo with { Id = feedInfoId });
    }

    [Fact]
    public async Task UpdateFeedInfoAsync_DifferentLanguage_LanguageChanged()
    {
        var handler = CreateGistHandler();
        var feedInfoToUpdate = CreateTestFeedInfo();
        var feedInfoId = await handler.InsertFeedInfoAsync(feedInfoToUpdate, CancellationToken.None);
        var expectedFeedInfo = feedInfoToUpdate with { Language = "different language" };

        await handler.UpdateFeedInfoAsync(expectedFeedInfo, CancellationToken.None);

        await GistAsserter.AssertFeedInfoIsInDbAsync(expectedFeedInfo with { Id = feedInfoId });
    }

    [Fact]
    public async Task UpdateFeedInfoAsync_FeedInfoDoesNotExist_ThrowsDatabaseOperationException()
    {
        var handler = CreateGistHandler();
        var feedInfoToUpdate = CreateTestFeedInfo();

        await Assert.ThrowsAsync<DatabaseOperationException>(() =>
            handler.UpdateFeedInfoAsync(feedInfoToUpdate, CancellationToken.None));
    }

    [Fact]
    public async Task GetFeedInfoByRssUrlAsync_FeedInfoDoesNotExist_Null()
    {
        var handler = CreateGistHandler();

        var actualFeedInfo = await handler.GetFeedInfoByRssUrlAsync("test rss url", CancellationToken.None);

        Assert.Null(actualFeedInfo);
    }

    [Fact]
    public async Task GetFeedInfoByRssUrlAsync_OnlyOneFeedInfoExists_CorrectFeedInfo()
    {
        var handler = CreateGistHandler();
        var expectedFeedInfo = CreateTestFeedInfo();
        var feedInfoId = await handler.InsertFeedInfoAsync(expectedFeedInfo, CancellationToken.None);

        var actualFeedInfo = await handler.GetFeedInfoByRssUrlAsync(expectedFeedInfo.RssUrl, CancellationToken.None);

        Assert.Equal(expectedFeedInfo with { Id = feedInfoId }, actualFeedInfo);
    }

    [Fact]
    public async Task GetFeedInfoByRssUrlAsync_MultipleFeedInfosExist_CorrectFeedInfo()
    {
        var handler = CreateGistHandler();
        await handler.InsertFeedInfoAsync(CreateTestFeedInfo(), CancellationToken.None);
        await handler.InsertFeedInfoAsync(CreateTestFeedInfo(), CancellationToken.None);
        var expectedFeedInfo = CreateTestFeedInfo();
        var feedInfoId = await handler.InsertFeedInfoAsync(expectedFeedInfo, CancellationToken.None);
        await handler.InsertFeedInfoAsync(CreateTestFeedInfo(), CancellationToken.None);
        await handler.InsertFeedInfoAsync(CreateTestFeedInfo(), CancellationToken.None);

        var actualFeedInfo = await handler.GetFeedInfoByRssUrlAsync(expectedFeedInfo.RssUrl, CancellationToken.None);

        Assert.Equal(expectedFeedInfo with { Id = feedInfoId }, actualFeedInfo);
    }

    [Fact]
    public async Task InsertGistAsync_GistDoesNotExist_GistIsInsertedInDb()
    {
        var handler = CreateGistHandler();
        var feedInfo = CreateTestFeedInfo();
        var feedInfoId = await handler.InsertFeedInfoAsync(feedInfo, CancellationToken.None);
        var gistToInsert = CreateTestGist(feedInfoId);

        await handler.InsertGistAsync(gistToInsert, CancellationToken.None);

        await GistAsserter.AssertGistIsInDbAsync(gistToInsert);
    }

    [Fact]
    public async Task InsertGistAsync_GistExistsAlready_ThrowsMySqlException()
    {
        var handler = CreateGistHandler();
        var feedInfo = CreateTestFeedInfo();
        var feedInfoId = await handler.InsertFeedInfoAsync(feedInfo, CancellationToken.None);
        var gistToInsert = CreateTestGist(feedInfoId);
        await handler.InsertGistAsync(gistToInsert, CancellationToken.None);

        await Assert.ThrowsAsync<MySqlException>(() => handler.InsertGistAsync(gistToInsert, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateGistAsync_EverythingDifferentExceptReference_InformationUpdated()
    {
        var handler = CreateGistHandler();
        var feedInfo = CreateTestFeedInfo();
        var feedInfoId = await handler.InsertFeedInfoAsync(feedInfo, CancellationToken.None);
        var existingGist = CreateTestGist(feedInfoId);
        var gistId = await handler.InsertGistAsync(existingGist, CancellationToken.None);
        var gistToUpdate = CreateTestGist(feedInfoId) with { Reference = existingGist.Reference };

        await handler.UpdateGistAsync(gistToUpdate, CancellationToken.None);

        await GistAsserter.AssertGistIsInDbAsync(gistToUpdate with { Id = gistId });
    }

    [Fact]
    public async Task UpdateGistAsync_GistDoesNotExist_ThrowsDatabaseOperationException()
    {
        var handler = CreateGistHandler();
        var feedInfo = CreateTestFeedInfo();
        var feedInfoId = await handler.InsertFeedInfoAsync(feedInfo, CancellationToken.None);
        var gistToUpdate = CreateTestGist(feedInfoId);

        await Assert.ThrowsAsync<DatabaseOperationException>(() =>
            handler.UpdateGistAsync(gistToUpdate, CancellationToken.None));
    }

    [Fact]
    public async Task GetGistByReferenceAsync_GistDoesNotExist_Null()
    {
        var handler = CreateGistHandler();

        var actualGist = await handler.GetGistByReferenceAsync("test reference", CancellationToken.None);

        Assert.Null(actualGist);
    }

    [Fact]
    public async Task GetGistByReferenceAsync_OnlyOneGistExists_CorrectGist()
    {
        var handler = CreateGistHandler();
        var feedInfo = CreateTestFeedInfo();
        var feedInfoId = await handler.InsertFeedInfoAsync(feedInfo, CancellationToken.None);
        var expectedGist = CreateTestGist(feedInfoId);
        var gistId = await handler.InsertGistAsync(expectedGist, CancellationToken.None);

        var actualGist = await handler.GetGistByReferenceAsync(expectedGist.Reference, CancellationToken.None);

        Assert.Equal(expectedGist with { Id = gistId }, actualGist);
    }

    [Fact]
    public async Task GetGistByReferenceAsync_MultipleGistsExist_CorrectGist()
    {
        var handler = CreateGistHandler();
        var feedInfo = CreateTestFeedInfo();
        var feedInfoId = await handler.InsertFeedInfoAsync(feedInfo, CancellationToken.None);
        await handler.InsertGistAsync(CreateTestGist(feedInfoId), CancellationToken.None);
        await handler.InsertGistAsync(CreateTestGist(feedInfoId), CancellationToken.None);
        var expectedGist = CreateTestGist(feedInfoId);
        var gistId = await handler.InsertGistAsync(expectedGist, CancellationToken.None);
        await handler.InsertGistAsync(CreateTestGist(feedInfoId), CancellationToken.None);
        await handler.InsertGistAsync(CreateTestGist(feedInfoId), CancellationToken.None);

        var actualGist = await handler.GetGistByReferenceAsync(expectedGist.Reference, CancellationToken.None);

        Assert.Equal(expectedGist with { Id = gistId }, actualGist);
    }

    [Fact]
    public async Task InsertSearchResultsAsync_NoSearchResultsExist_SearchResultsInserted()
    {
        var handler = CreateGistHandler();
        var feedInfoId = await handler.InsertFeedInfoAsync(CreateTestFeedInfo(), CancellationToken.None);
        var gistId = await handler.InsertGistAsync(CreateTestGist(feedInfoId), CancellationToken.None);
        var searchResultsToInsert = Enumerable.Repeat(gistId, 3).Select(CreateTestSearchResult).ToArray();

        await handler.InsertSearchResultsAsync(searchResultsToInsert, CancellationToken.None);

        await GistAsserter.AssertSearchResultsForGistIdInDbAsync(gistId, searchResultsToInsert);
    }

    [Fact]
    public async Task InsertSearchResultsAsync_SearchResultsForSameGistExist_SearchResultsInsertedAdditionally()
    {
        var handler = CreateGistHandler();
        var feedInfoId = await handler.InsertFeedInfoAsync(CreateTestFeedInfo(), CancellationToken.None);
        var gistId = await handler.InsertGistAsync(CreateTestGist(feedInfoId), CancellationToken.None);
        var existingSearchResults = Enumerable.Repeat(gistId, 3).Select(CreateTestSearchResult).ToArray();
        await handler.InsertSearchResultsAsync(existingSearchResults, CancellationToken.None);
        var searchResultsToInsert = Enumerable.Repeat(gistId, 3).Select(CreateTestSearchResult).ToArray();

        await handler.InsertSearchResultsAsync(searchResultsToInsert, CancellationToken.None);

        var expectedSearchResults = existingSearchResults.Concat(searchResultsToInsert);
        await GistAsserter.AssertSearchResultsForGistIdInDbAsync(gistId, expectedSearchResults);
    }

    [Fact]
    public async Task UpdateSearchResultsAsync_SearchResultsForSameGistExist_OnlyUpdatedSearchResultsInDb()
    {
        var handler = CreateGistHandler();
        var feedInfoId = await handler.InsertFeedInfoAsync(CreateTestFeedInfo(), CancellationToken.None);
        var gistId = await handler.InsertGistAsync(CreateTestGist(feedInfoId), CancellationToken.None);
        var existingSearchResults = Enumerable.Repeat(gistId, 3).Select(CreateTestSearchResult).ToArray();
        await handler.InsertSearchResultsAsync(existingSearchResults, CancellationToken.None);
        var searchResultsToUpdate = Enumerable.Repeat(gistId, 3).Select(CreateTestSearchResult).ToArray();

        await handler.InsertSearchResultsAsync(searchResultsToUpdate, CancellationToken.None);

        await GistAsserter.AssertSearchResultsForGistIdInDbAsync(gistId, searchResultsToUpdate);
    }

    [Fact]
    public async Task UpdateSearchResultAsync_NoSearchResultsExist_ThrowsDatabaseOperationException()
    {
        var handler = CreateGistHandler();
        var feedInfoId = await handler.InsertFeedInfoAsync(CreateTestFeedInfo(), CancellationToken.None);
        var gistId = await handler.InsertGistAsync(CreateTestGist(feedInfoId), CancellationToken.None);
        var searchResultsToUpdate = Enumerable.Repeat(gistId, 3).Select(CreateTestSearchResult).ToArray();

        await Assert.ThrowsAsync<DatabaseOperationException>(() =>
            handler.UpdateSearchResultsAsync(searchResultsToUpdate, CancellationToken.None));
    }

    [Fact]
    public async Task DailyRecapExistsAsync_NoRecapsExist_False()
    {
        var handler = CreateRecapHandler();

        var actual = await handler.DailyRecapExistsAsync(CancellationToken.None);

        Assert.False(actual);
    }

    [Fact]
    public async Task DailyRecapExistsAsync_OneRecapInLast24HoursExists_True()
    {
        var testCreated = _random.NextDateTime();
        var dateTimeHandlerMock = Substitute.For<IDateTimeHandler>();
        var handler = CreateRecapHandler(dateTimeHandlerMock);
        dateTimeHandlerMock.GetUtcNow().Returns(testCreated);
        await handler.InsertDailyRecapAsync(CreateTestRecap(), CancellationToken.None);
        var newerTestCreated = testCreated.AddDays(1);
        dateTimeHandlerMock.GetUtcNow().Returns(newerTestCreated);
        await handler.InsertDailyRecapAsync(CreateTestRecap(), CancellationToken.None);
        dateTimeHandlerMock.GetUtcNow().Returns(newerTestCreated.AddDays(0.5));

        var actual = await handler.DailyRecapExistsAsync(CancellationToken.None);

        Assert.True(actual);
    }

    [Fact]
    public async Task DailyRecapExistsAsync_TwoRecapsInLast24HoursExist_DatabaseOperationException()
    {
        var testCreated = _random.NextDateTime();
        var dateTimeHandlerMock = Substitute.For<IDateTimeHandler>();
        var handler = CreateRecapHandler(dateTimeHandlerMock);
        dateTimeHandlerMock.GetUtcNow().Returns(testCreated);
        await handler.InsertDailyRecapAsync(CreateTestRecap(), CancellationToken.None);
        var newerTestCreated = testCreated.AddDays(0.25);
        dateTimeHandlerMock.GetUtcNow().Returns(newerTestCreated);
        await handler.InsertDailyRecapAsync(CreateTestRecap(), CancellationToken.None);
        dateTimeHandlerMock.GetUtcNow().Returns(newerTestCreated.AddDays(0.25));

        await Assert.ThrowsAsync<DatabaseOperationException>(
            () => handler.DailyRecapExistsAsync(CancellationToken.None));
    }

    [Fact]
    public async Task WeeklyRecapExistsAsync_NoRecapsExist_False()
    {
        var handler = CreateRecapHandler();

        var actual = await handler.WeeklyRecapExistsAsync(CancellationToken.None);

        Assert.False(actual);
    }

    [Fact]
    public async Task WeeklyRecapExistsAsync_OneRecapInLast24HoursExists_True()
    {
        var testCreated = _random.NextDateTime();
        var dateTimeHandlerMock = Substitute.For<IDateTimeHandler>();
        var handler = CreateRecapHandler(dateTimeHandlerMock);
        dateTimeHandlerMock.GetUtcNow().Returns(testCreated);
        await handler.InsertWeeklyRecapAsync(CreateTestRecap(), CancellationToken.None);
        var newerTestCreated = testCreated.AddDays(1);
        dateTimeHandlerMock.GetUtcNow().Returns(newerTestCreated);
        await handler.InsertWeeklyRecapAsync(CreateTestRecap(), CancellationToken.None);
        dateTimeHandlerMock.GetUtcNow().Returns(newerTestCreated.AddDays(0.5));

        var actual = await handler.WeeklyRecapExistsAsync(CancellationToken.None);

        Assert.True(actual);
    }

    [Fact]
    public async Task WeeklyRecapExistsAsync_TwoRecapsInLast24HoursExist_DatabaseOperationException()
    {
        var testCreated = _random.NextDateTime();
        var dateTimeHandlerMock = Substitute.For<IDateTimeHandler>();
        var handler = CreateRecapHandler(dateTimeHandlerMock);
        dateTimeHandlerMock.GetUtcNow().Returns(testCreated);
        await handler.InsertWeeklyRecapAsync(CreateTestRecap(), CancellationToken.None);
        var newerTestCreated = testCreated.AddDays(0.25);
        dateTimeHandlerMock.GetUtcNow().Returns(newerTestCreated);
        await handler.InsertWeeklyRecapAsync(CreateTestRecap(), CancellationToken.None);
        dateTimeHandlerMock.GetUtcNow().Returns(newerTestCreated.AddDays(0.25));

        await Assert.ThrowsAsync<DatabaseOperationException>(
            () => handler.WeeklyRecapExistsAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GetGistsOfLastDayAsync_NoGistsExist_EmptyList()
    {
        var dateTimeHandlerMock = Substitute.For<IDateTimeHandler>();
        dateTimeHandlerMock.GetUtcNow().Returns(_random.NextDateTime());
        var handler = CreateRecapHandler(dateTimeHandlerMock);

        var actual = await handler.GetGistsOfLastDayAsync(CancellationToken.None);

        Assert.Empty(actual);
    }

    [Fact]
    public async Task GetGistsOfLastDayAsync_GistsExist_ListWithOnlyNewerGist()
    {
        var testNow = _random.NextDateTime();
        var gistHandler = CreateGistHandler();
        var feedInfo = CreateTestFeedInfo();
        var feedInfoId = await gistHandler.InsertFeedInfoAsync(feedInfo, CancellationToken.None);
        var testGist = CreateTestGist(feedInfoId) with {
            Published = testNow.AddHours(-12),
            Updated = testNow.AddHours(-12)
        };
        await gistHandler.InsertGistAsync(testGist, CancellationToken.None);
        var olderTestGist = CreateTestGist(feedInfoId) with {
            Published = testNow.AddHours(-36),
            Updated = testNow.AddHours(-36)
        };
        await gistHandler.InsertGistAsync(olderTestGist, CancellationToken.None);
        var dateTimeHandlerMock = Substitute.For<IDateTimeHandler>();
        dateTimeHandlerMock.GetUtcNow().Returns(testNow);
        var recapHandler = CreateRecapHandler(dateTimeHandlerMock);

        var actual = await recapHandler.GetGistsOfLastDayAsync(CancellationToken.None);

        Assert.Single(actual);
        Assert.Equal(testGist with { Id = actual.Single().Id }, actual.Single());
    }

    [Fact]
    public async Task GetGistsOfLastWeekAsync_NoGistsExist_EmptyList()
    {
        var dateTimeHandlerMock = Substitute.For<IDateTimeHandler>();
        dateTimeHandlerMock.GetUtcNow().Returns(_random.NextDateTime());
        var handler = CreateRecapHandler(dateTimeHandlerMock);

        var actual = await handler.GetGistsOfLastWeekAsync(CancellationToken.None);

        Assert.Empty(actual);
    }

    [Fact]
    public async Task GetGistsOfLastWeekAsync_GistsExist_ListWithOnlyNewerGist()
    {
        var testNow = _random.NextDateTime();
        var gistHandler = CreateGistHandler();
        var feedInfo = CreateTestFeedInfo();
        var feedInfoId = await gistHandler.InsertFeedInfoAsync(feedInfo, CancellationToken.None);
        var testGist = CreateTestGist(feedInfoId) with {
            Published = testNow.AddDays(-3),
            Updated = testNow.AddDays(-3)
        };
        await gistHandler.InsertGistAsync(testGist, CancellationToken.None);
        var olderTestGist = CreateTestGist(feedInfoId) with {
            Published = testNow.AddDays(-10),
            Updated = testNow.AddDays(-10)
        };
        await gistHandler.InsertGistAsync(olderTestGist, CancellationToken.None);
        var dateTimeHandlerMock = Substitute.For<IDateTimeHandler>();
        dateTimeHandlerMock.GetUtcNow().Returns(testNow);
        var recapHandler = CreateRecapHandler(dateTimeHandlerMock);

        var actual = await recapHandler.GetGistsOfLastWeekAsync(CancellationToken.None);

        Assert.Single(actual);
        Assert.Equal(testGist with { Id = actual.Single().Id }, actual.Single());
    }

    [Fact]
    public async Task InsertDailyRecapAsync_NormalRecap_RecapInserted()
    {
        var testCreated = _random.NextDateTime();
        var dateTimeHandlerMock = Substitute.For<IDateTimeHandler>();
        dateTimeHandlerMock.GetUtcNow().Returns(testCreated);
        var handler = CreateRecapHandler(dateTimeHandlerMock);
        var recapToInsert = CreateTestRecap();

        await handler.InsertDailyRecapAsync(recapToInsert, CancellationToken.None);

        await RecapAsserter.AssertRecapIsInDbAsync(recapToInsert, testCreated, RecapType.Daily);
    }

    [Fact]
    public async Task InsertWeeklyRecapAsync_NormalRecap_RecapInserted()
    {
        var testCreated = _random.NextDateTime();
        var dateTimeHandlerMock = Substitute.For<IDateTimeHandler>();
        dateTimeHandlerMock.GetUtcNow().Returns(testCreated);
        var handler = CreateRecapHandler(dateTimeHandlerMock);
        var recapToInsert = CreateTestRecap();

        await handler.InsertWeeklyRecapAsync(recapToInsert, CancellationToken.None);

        await RecapAsserter.AssertRecapIsInDbAsync(recapToInsert, testCreated, RecapType.Weekly);
    }

    [Fact]
    public async Task GetAllGistsAsync_NoGistsExist_EmptyList()
    {
        var handler = CreateGistHandler();
        var feedInfo = CreateTestFeedInfo();
        var feedInfoId = await handler.InsertFeedInfoAsync(feedInfo, CancellationToken.None);

        var actual = await handler.GetAllGistsAsync([feedInfoId], CancellationToken.None);

        Assert.Empty(actual);
    }

    [Fact]
    public async Task GetAllGistsAsync_GistsExist_ListWithAllGists()
    {
        var handler = CreateGistHandler();
        var feedInfo = CreateTestFeedInfo();
        var feedId = await handler.InsertFeedInfoAsync(feedInfo, CancellationToken.None);
        var otherFeedInfo = CreateTestFeedInfo();
        var otherFeedId = await handler.InsertFeedInfoAsync(otherFeedInfo, CancellationToken.None);
        var expected = new List<Gist> {
            CreateTestGist(feedId),
            CreateTestGist(feedId),
            CreateTestGist(otherFeedId)
        };
        await Task.WhenAll(expected.Select(gist => handler.InsertGistAsync(gist, CancellationToken.None)));

        var actual = await handler.GetAllGistsAsync([feedId, otherFeedId], CancellationToken.None);

        actual.ForEach(gist => gist.Id = null);
        Assert.Equivalent(expected, actual);
    }

    [Fact]
    public async Task EnsureCorrectDisabledStateForGistAsync_GistIsEnabledAndShouldBeDisabled_FalseAndGistIsDisabled()
    {
        var handler = CreateGistHandler();
        var feedInfo = CreateTestFeedInfo();
        var feedId = await handler.InsertFeedInfoAsync(feedInfo, CancellationToken.None);
        var gistToInsert = CreateTestGist(feedId);
        var gistId = await handler.InsertGistAsync(gistToInsert, CancellationToken.None);

        var actual = await handler.EnsureCorrectDisabledStateForGistAsync(gistId, true, CancellationToken.None);

        await GistAsserter.AssertGistIsDisabledAsync(gistId);
        Assert.False(actual);
    }

    [Fact]
    public async Task EnsureCorrectDisabledStateForGistAsync_GistIsDisabledAndShouldBeDisabled_TrueAndGistIsDisabled()
    {
        var handler = CreateGistHandler();
        var feedInfo = CreateTestFeedInfo();
        var feedInfoId = await handler.InsertFeedInfoAsync(feedInfo, CancellationToken.None);
        var gistToInsert = CreateTestGist(feedInfoId);
        var gistId = await handler.InsertGistAsync(gistToInsert, CancellationToken.None);
        await handler.EnsureCorrectDisabledStateForGistAsync(gistId, true, CancellationToken.None);

        var actual = await handler.EnsureCorrectDisabledStateForGistAsync(gistId, true, CancellationToken.None);

        await GistAsserter.AssertGistIsDisabledAsync(gistId);
        Assert.True(actual);
    }

    [Fact]
    public async Task EnsureCorrectDisabledStateForGistAsync_GistIsEnabledAndShouldBeEnabled_TrueAndGistIsEnabled()
    {
        var handler = CreateGistHandler();
        var feedInfo = CreateTestFeedInfo();
        var feedInfoId = await handler.InsertFeedInfoAsync(feedInfo, CancellationToken.None);
        var gistToInsert = CreateTestGist(feedInfoId);
        var gistId = await handler.InsertGistAsync(gistToInsert, CancellationToken.None);

        var actual = await handler.EnsureCorrectDisabledStateForGistAsync(gistId, false, CancellationToken.None);

        await GistAsserter.AssertGistIsEnabledAsync(gistId);
        Assert.True(actual);
    }

    [Fact]
    public async Task EnsureCorrectDisabledStateForGistAsync_GistIsDisabledAndShouldBeEnabled_FalseAndGistIsEnabled()
    {
        var handler = CreateGistHandler();
        var feedInfo = CreateTestFeedInfo();
        var feedInfoId = await handler.InsertFeedInfoAsync(feedInfo, CancellationToken.None);
        var gistToInsert = CreateTestGist(feedInfoId);
        var gistId = await handler.InsertGistAsync(gistToInsert, CancellationToken.None);
        await handler.EnsureCorrectDisabledStateForGistAsync(gistId, true, CancellationToken.None);

        var actual = await handler.EnsureCorrectDisabledStateForGistAsync(gistId, false, CancellationToken.None);

        await GistAsserter.AssertGistIsEnabledAsync(gistId);
        Assert.False(actual);
    }

    private MariaDbHandler CreateGistHandler() =>
        new(Options.Create(_gistHandlerOptions), new DateTimeHandler(), null);

    private MariaDbHandler CreateRecapHandler(IDateTimeHandler? dateTimeHandler = null) =>
        new(Options.Create(_recapHandlerOptions), dateTimeHandler ?? new DateTimeHandler(), null);

    private RssFeedInfo CreateTestFeedInfo() => new(
        _random.NextString(),
        _random.NextString(),
        _random.NextString()
    );

    private Gist CreateTestGist(int feedId) => new(
        _random.NextString(),
        feedId,
        _random.NextString(),
        _random.NextString(),
        _random.NextDateTime(max: DateTime.UnixEpoch.AddYears(30)),
        _random.NextDateTime(min: DateTime.UnixEpoch.AddYears(30)),
        _random.NextString(),
        _random.NextString(),
        string.Join(";;", _random.NextArrayOfStrings()),
        _random.NextString()
    );

    private GoogleSearchResult CreateTestSearchResult(int gistId) => new(
        gistId,
        _random.NextString(),
        _random.NextString(),
        _random.NextString(),
        _random.NextString(),
        _random.NextString()
    );

    private List<CategoryRecap> CreateTestRecap() => Enumerable.Range(0, 5).Select(_ =>
        new CategoryRecap(
            _random.NextString(),
            _random.NextString(),
            Enumerable.Range(0, 3).Select(_ => _random.Next(10000))
        )
    ).ToList();
}
