using System.Text.Json;
using GistBackend.Exceptions;
using GistBackend.Handlers;
using GistBackend.Handlers.MariaDbHandler;
using GistBackend.IntegrationTest.Utils;
using GistBackend.Types;
using GistBackend.Utils;
using Microsoft.Extensions.Options;
using MySqlConnector;
using NSubstitute;
using static TestUtilities.TestData;

namespace GistBackend.IntegrationTest;

[Collection(nameof(TestsWithoutParallelizationCollection))]
public class MariaDbHandlerTests : IClassFixture<MariaDbFixture>
{
    private readonly Random _random = new();
    private readonly MariaDbHandlerOptions _gistHandlerOptions;
    private readonly MariaDbHandlerOptions _recapHandlerOptions;
    private readonly MariaDbHandlerOptions _cleanupHandlerOptions;
    private readonly MariaDbHandlerOptions _gistControllerHandlerOptions;
    private readonly MariaDbHandlerOptions _telegramHandlerOptions;

    public MariaDbHandlerTests(MariaDbFixture fixture)
    {
        _gistHandlerOptions = new MariaDbHandlerOptions
        {
            Server = fixture.Hostname,
            User = MariaDbFixture.GistServiceDbUsername,
            Password = MariaDbFixture.GistServiceDbPassword,
            Port = fixture.ExposedPort
        };
        _recapHandlerOptions = new MariaDbHandlerOptions
        {
            Server = fixture.Hostname,
            User = MariaDbFixture.RecapServiceDbUsername,
            Password = MariaDbFixture.RecapServiceDbPassword,
            Port = fixture.ExposedPort
        };
        _cleanupHandlerOptions = new MariaDbHandlerOptions
        {
            Server = fixture.Hostname,
            User = MariaDbFixture.CleanupServiceDbUsername,
            Password = MariaDbFixture.CleanupServiceDbPassword,
            Port = fixture.ExposedPort
        };
        _gistControllerHandlerOptions = new MariaDbHandlerOptions
        {
            Server = fixture.Hostname,
            User = MariaDbFixture.GistsControllerDbUsername,
            Password = MariaDbFixture.GistsControllerDbPassword,
            Port = fixture.ExposedPort
        };
        _telegramHandlerOptions = new MariaDbHandlerOptions
        {
            Server = fixture.Hostname,
            User = MariaDbFixture.TelegramServiceDbUsername,
            Password = MariaDbFixture.TelegramServiceDbPassword,
            Port = fixture.ExposedPort
        };
        fixture.ClearDatabaseAsync().GetAwaiter().GetResult();
    }

    private MariaDbAsserter GistAsserter => new(_gistHandlerOptions);
    private MariaDbAsserter RecapAsserter => new(_recapHandlerOptions);
    private MariaDbAsserter ChatAsserter => new(_telegramHandlerOptions);

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

        var actualFeedInfo = await handler.GetFeedInfoByRssUrlAsync(new Uri("http://test.rss.url/"), CancellationToken.None);

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
        var expected = (await handler.InsertTestGistsAsync(1)).Single();

        var actual = await handler.GetGistByReferenceAsync(expected.Reference, CancellationToken.None);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task GetGistByReferenceAsync_MultipleGistsExist_CorrectGist()
    {
        var handler = CreateGistHandler();
        var expected = (await handler.InsertTestGistsAsync(5))[2];

        var actual = await handler.GetGistByReferenceAsync(expected.Reference, CancellationToken.None);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task InsertSearchResultsAsync_NoSearchResultsExist_SearchResultsInserted()
    {
        var handler = CreateGistHandler();
        var feedInfoId = await handler.InsertFeedInfoAsync(CreateTestFeedInfo(), CancellationToken.None);
        var gistId = await handler.InsertGistAsync(CreateTestGist(feedInfoId), CancellationToken.None);
        var searchResultsToInsert = Enumerable.Repeat(gistId, 3).Select(id => CreateTestSearchResult(id)).ToList();

        await handler.InsertSearchResultsAsync(searchResultsToInsert, CancellationToken.None);

        await GistAsserter.AssertSearchResultsForGistIdInDbAsync(gistId, searchResultsToInsert);
    }

    [Fact]
    public async Task InsertSearchResultsAsync_SearchResultsForSameGistExist_SearchResultsInsertedAdditionally()
    {
        var handler = CreateGistHandler();
        var feedInfoId = await handler.InsertFeedInfoAsync(CreateTestFeedInfo(), CancellationToken.None);
        var gistId = await handler.InsertGistAsync(CreateTestGist(feedInfoId), CancellationToken.None);
        var existingSearchResults = Enumerable.Repeat(gistId, 3).Select(id => CreateTestSearchResult(id)).ToList();
        await handler.InsertSearchResultsAsync(existingSearchResults, CancellationToken.None);
        var searchResultsToInsert = Enumerable.Repeat(gistId, 3).Select(id => CreateTestSearchResult(id)).ToList();

        await handler.InsertSearchResultsAsync(searchResultsToInsert, CancellationToken.None);

        var expectedSearchResults = existingSearchResults.Concat(searchResultsToInsert).ToList();
        await GistAsserter.AssertSearchResultsForGistIdInDbAsync(gistId, expectedSearchResults);
    }

    [Fact]
    public async Task UpdateSearchResultsAsync_SearchResultsForSameGistExist_OnlyUpdatedSearchResultsInDb()
    {
        var handler = CreateGistHandler();
        var feedInfoId = await handler.InsertFeedInfoAsync(CreateTestFeedInfo(), CancellationToken.None);
        var gistId = await handler.InsertGistAsync(CreateTestGist(feedInfoId), CancellationToken.None);
        var existingSearchResults = Enumerable.Repeat(gistId, 3).Select(id => CreateTestSearchResult(id)).ToList();
        await handler.InsertSearchResultsAsync(existingSearchResults, CancellationToken.None);
        var searchResultsToUpdate = Enumerable.Repeat(gistId, 3).Select(id => CreateTestSearchResult(id)).ToList();

        await handler.UpdateSearchResultsAsync(searchResultsToUpdate, CancellationToken.None);

        await GistAsserter.AssertSearchResultsForGistIdInDbAsync(gistId, searchResultsToUpdate);
    }

    [Fact]
    public async Task UpdateSearchResultAsync_NoSearchResultsExist_ThrowsDatabaseOperationException()
    {
        var handler = CreateGistHandler();
        var feedInfoId = await handler.InsertFeedInfoAsync(CreateTestFeedInfo(), CancellationToken.None);
        var gistId = await handler.InsertGistAsync(CreateTestGist(feedInfoId), CancellationToken.None);
        var searchResultsToUpdate = Enumerable.Repeat(gistId, 3).Select(id => CreateTestSearchResult(id)).ToList();

        await Assert.ThrowsAsync<DatabaseOperationException>(() =>
            handler.UpdateSearchResultsAsync(searchResultsToUpdate, CancellationToken.None));
    }

    [Fact]
    public async Task GetSearchResultsByGistIdAsync_NoSearchResultsExist_EmptyList()
    {
        var handler = CreateGistHandler();
        var feedInfoId = await handler.InsertFeedInfoAsync(CreateTestFeedInfo(), CancellationToken.None);
        var gistId = await handler.InsertGistAsync(CreateTestGist(feedInfoId), CancellationToken.None);

        var actual = await handler.GetSearchResultsByGistIdAsync(gistId, CancellationToken.None);

        Assert.Empty(actual);
    }

    [Fact]
    public async Task GetSearchResultsByGistIdAsync_SearchResultsExist_ListWithSearchResults()
    {
        var gistHandler = CreateGistHandler();
        var feedInfoId = await gistHandler.InsertFeedInfoAsync(CreateTestFeedInfo(), CancellationToken.None);
        var gistId = await gistHandler.InsertGistAsync(CreateTestGist(feedInfoId), CancellationToken.None);
        var expected = await gistHandler.InsertTestSearchResultsAsync(3, gistId);
        var gistControllerHandler = CreateGistControllerHandler();

        var actual = await gistControllerHandler.GetSearchResultsByGistIdAsync(gistId, CancellationToken.None);

        Assert.Equivalent(expected, actual);
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
        var handler = CreateCleanupHandler();

        var actual = await handler.GetAllGistsAsync(CancellationToken.None);

        Assert.Empty(actual);
    }

    [Fact]
    public async Task GetAllGistsAsync_GistsExistInOneFeed_ListWithAllGists()
    {
        var gistHandler = CreateGistHandler();
        var expected = (await gistHandler.InsertTestGistsAsync(10))
            .Concat(await gistHandler.InsertTestGistsAsync(10))
            .Concat(await gistHandler.InsertTestGistsAsync(10))
            .ToList();
        var gistsControllerHandler = CreateCleanupHandler();

        var actual = await gistsControllerHandler.GetAllGistsAsync(CancellationToken.None);

        Assert.Equal(expected.OrderBy(g => g.Id), actual.OrderBy(g => g.Id));
    }

    [Fact]
    public async Task EnsureCorrectDisabledStateForGistAsync_GistIsEnabledAndShouldBeDisabled_FalseAndGistIsDisabled()
    {
        var gistHandler = CreateGistHandler();
        var gistId = (await gistHandler.InsertTestGistsAsync(1)).Single().Id!.Value;
        var cleanupHandler = CreateCleanupHandler();

        var actual = await cleanupHandler.EnsureCorrectDisabledStateForGistAsync(gistId, true, CancellationToken.None);

        await GistAsserter.AssertGistIsDisabledAsync(gistId);
        Assert.False(actual);
    }

    [Fact]
    public async Task EnsureCorrectDisabledStateForGistAsync_GistIsDisabledAndShouldBeDisabled_TrueAndGistIsDisabled()
    {
        var gistHandler = CreateGistHandler();
        var gistId = (await gistHandler.InsertTestGistsAsync(1)).Single().Id!.Value;
        var cleanupHandler = CreateCleanupHandler();
        await cleanupHandler.EnsureCorrectDisabledStateForGistAsync(gistId, true, CancellationToken.None);

        var actual = await cleanupHandler.EnsureCorrectDisabledStateForGistAsync(gistId, true, CancellationToken.None);

        await GistAsserter.AssertGistIsDisabledAsync(gistId);
        Assert.True(actual);
    }

    [Fact]
    public async Task EnsureCorrectDisabledStateForGistAsync_GistIsEnabledAndShouldBeEnabled_TrueAndGistIsEnabled()
    {
        var gistHandler = CreateGistHandler();
        var feedInfo = CreateTestFeedInfo();
        var feedInfoId = await gistHandler.InsertFeedInfoAsync(feedInfo, CancellationToken.None);
        var gistToInsert = CreateTestGist(feedInfoId);
        var gistId = await gistHandler.InsertGistAsync(gistToInsert, CancellationToken.None);
        var cleanupHandler = CreateCleanupHandler();

        var actual = await cleanupHandler.EnsureCorrectDisabledStateForGistAsync(gistId, false, CancellationToken.None);

        await GistAsserter.AssertGistIsEnabledAsync(gistId);
        Assert.True(actual);
    }

    [Fact]
    public async Task EnsureCorrectDisabledStateForGistAsync_GistIsDisabledAndShouldBeEnabled_FalseAndGistIsEnabled()
    {
        var gistHandler = CreateGistHandler();
        var feedInfo = CreateTestFeedInfo();
        var feedInfoId = await gistHandler.InsertFeedInfoAsync(feedInfo, CancellationToken.None);
        var gistToInsert = CreateTestGist(feedInfoId);
        var gistId = await gistHandler.InsertGistAsync(gistToInsert, CancellationToken.None);
        var cleanupHandler = CreateCleanupHandler();
        await cleanupHandler.EnsureCorrectDisabledStateForGistAsync(gistId, true, CancellationToken.None);

        var actual = await cleanupHandler.EnsureCorrectDisabledStateForGistAsync(gistId, false, CancellationToken.None);

        await GistAsserter.AssertGistIsEnabledAsync(gistId);
        Assert.False(actual);
    }

    [Fact]
    public async Task GetPreviousGistsWithFeedAsync_NoGists_EmptyList()
    {
        var handler = CreateGistControllerHandler();

        var gists = await handler.GetPreviousGistsWithFeedAsync(10, null, [], null, [], CancellationToken.None);

        Assert.Empty(gists);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(8)]
    public async Task GetPreviousGistsWithFeedAsync_LessGistsThanTake_AllGists(int gistCount)
    {
        var gistHandler = CreateGistHandler();
        var testFeed = (await gistHandler.InsertTestFeedInfosAsync(1)).Single();
        var testGists = await gistHandler.InsertTestGistsAsync(gistCount, testFeed.Id);
        var expectedGistsWithFeed = testGists.Select(gist => GistWithFeed.FromGistAndFeed(gist, testFeed)).ToList();
        var gistsControllerHandler = CreateGistControllerHandler();

        var actualGistsWithFeed =
            await gistsControllerHandler.GetPreviousGistsWithFeedAsync(gistCount + 5, null, [], null, [],
                CancellationToken.None);

        Assert.Equivalent(expectedGistsWithFeed, actualGistsWithFeed);
    }

    [Fact]
    public async Task GetPreviousGistsWithFeedAsync_MoreGistsThanTake_AsManyGistsAsExpected()
    {
        var gistHandler = CreateGistHandler();
        const int take = 5;
        var testFeed = (await gistHandler.InsertTestFeedInfosAsync(1)).Single();
        var testGists = await gistHandler.InsertTestGistsAsync(take+5, testFeed.Id);
        var expectedGistsWithFeed =
            testGists.Take(take).Select(gist => GistWithFeed.FromGistAndFeed(gist, testFeed)).ToList();
        var gistsControllerHandler = CreateGistControllerHandler();

        var actualGistsWithFeed =
            await gistsControllerHandler.GetPreviousGistsWithFeedAsync(take, null, [], null, [],
                CancellationToken.None);

        Assert.Equivalent(expectedGistsWithFeed, actualGistsWithFeed);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(8)]
    public async Task GetPreviousGistsWithFeedAsync_LastGistGivenId_GistsAfterLastGistId(int take)
    {
        var gistHandler = CreateGistHandler();
        var testFeed = (await gistHandler.InsertTestFeedInfosAsync(1)).Single();
        var testGists = await gistHandler.InsertTestGistsAsync(10, testFeed.Id);
        var firstHalfOfGists = testGists.Skip(5).ToList();
        var lastGistId = testGists[4].Id;
        var expectedGistsWithFeed = firstHalfOfGists.Take(take)
            .Select(gist => GistWithFeed.FromGistAndFeed(gist, testFeed)).ToList();
        var gistsControllerHandler = CreateGistControllerHandler();

        var actualGistsWithFeed =
            await gistsControllerHandler.GetPreviousGistsWithFeedAsync(take, lastGistId, [], null, [], CancellationToken.None);

        Assert.Equivalent(expectedGistsWithFeed, actualGistsWithFeed);
    }

    [Fact]
    public async Task GetPreviousGistsWithFeedAsync_QuerySpecificTags_GistsWithAllTags()
    {
        var tags = new[] { "tag1", "tag2", "tag3" };
        var gistHandler = CreateGistHandler();
        var testFeed = (await gistHandler.InsertTestFeedInfosAsync(1)).Single();
        var gistWithoutExpectedTags = CreateTestGist(testFeed.Id);
        var gistWithOnlyOneExpectedTag = CreateTestGist(testFeed.Id) with {
            Tags = string.Join(";;", tags.First())
        };
        var gistWithExpectedTags = CreateTestGist(testFeed.Id) with { Tags = string.Join(";;", tags) };
        var gistWithExpectedAndOtherTags = CreateTestGist(testFeed.Id) with {
            Tags = string.Join(";;", tags.Concat(_random.NextArrayOfStrings(3)))
        };
        await gistHandler.InsertGistAsync(gistWithoutExpectedTags, CancellationToken.None);
        await gistHandler.InsertGistAsync(gistWithOnlyOneExpectedTag, CancellationToken.None);
        gistWithExpectedTags.Id = await gistHandler.InsertGistAsync(gistWithExpectedTags, CancellationToken.None);
        gistWithExpectedAndOtherTags.Id =
            await gistHandler.InsertGistAsync(gistWithExpectedAndOtherTags, CancellationToken.None);
        var expectedGistsWithFeed = new List<GistWithFeed>
        {
            GistWithFeed.FromGistAndFeed(gistWithExpectedTags, testFeed),
            GistWithFeed.FromGistAndFeed(gistWithExpectedAndOtherTags, testFeed)
        };
        var gistsControllerHandler = CreateGistControllerHandler();

        var actualGistsWithFeed =
            await gistsControllerHandler.GetPreviousGistsWithFeedAsync(10, null, tags, null, [], CancellationToken.None);

        Assert.Equivalent(expectedGistsWithFeed, actualGistsWithFeed);
    }

    [Fact]
    public async Task GetPreviousGistsWithFeedAsync_QueryWordsFromTitleAndSummary_GistsWithAllWords()
    {
        var words = new[] { "word1", "word2", "word3" };
        var gistHandler = CreateGistHandler();
        var testFeed = (await gistHandler.InsertTestFeedInfosAsync(1)).Single();
        var gistWithoutExpectedWords = CreateTestGist(testFeed.Id);
        var gistWithOnlyOneExpectedWordInTitle = CreateTestGist(testFeed.Id) with {
            Title = $"This is a {words.First()} title"
        };
        var gistWithAllExpectedWordsInTitle = CreateTestGist(testFeed.Id) with {
            Title = $"This is a {words[0]}someextratext and {words[1]}{words[2]} title"
        };
        var gistWithAllExpectedWordsInSummary = CreateTestGist(testFeed.Id) with {
            Summary = $"This is a {words[0]}someextratext and {words[1]}{words[2]} summary"
        };
        var gistWithAllExpectedWords = CreateTestGist(testFeed.Id) with {
            Title = $"This is a {words[0]}someextratext title",
            Summary = $"This is a {words[1]}{words[2]} summary"
        };
        await gistHandler.InsertGistAsync(gistWithoutExpectedWords, CancellationToken.None);
        await gistHandler.InsertGistAsync(gistWithOnlyOneExpectedWordInTitle, CancellationToken.None);
        gistWithAllExpectedWordsInTitle.Id =
            await gistHandler.InsertGistAsync(gistWithAllExpectedWordsInTitle, CancellationToken.None);
        gistWithAllExpectedWordsInSummary.Id =
            await gistHandler.InsertGistAsync(gistWithAllExpectedWordsInSummary, CancellationToken.None);
        gistWithAllExpectedWords.Id =
            await gistHandler.InsertGistAsync(gistWithAllExpectedWords, CancellationToken.None);
        var expectedGists = new List<Gist>
            { gistWithAllExpectedWords, gistWithAllExpectedWordsInTitle, gistWithAllExpectedWordsInSummary };
        var expectedGistsWithFeed = expectedGists.Select(gist => GistWithFeed.FromGistAndFeed(gist, testFeed)).ToList();
        var searchQuery = string.Join(' ', words);
        var gistsControllerHandler = CreateGistControllerHandler();

        var actualGistsWithFeed =
            await gistsControllerHandler.GetPreviousGistsWithFeedAsync(10, null, [], searchQuery, [], CancellationToken.None);

        Assert.Equivalent(expectedGistsWithFeed, actualGistsWithFeed);
    }

    [Fact]
    public async Task GetPreviousGistsWithFeedAsync_GistsFromDisabledFeedInDb_OnlyGistsFromEnabledFeeds()
    {
        var gistHandler = CreateGistHandler();
        var gistsFromDisabledFeed = await gistHandler.InsertTestGistsAsync(5);
        var gistsFromOtherDisabledFeed = await gistHandler.InsertTestGistsAsync(5);
        var enabledFeed = (await gistHandler.InsertTestFeedInfosAsync(1)).Single();
        var gistsFromEnabledFeed = await gistHandler.InsertTestGistsAsync(5, enabledFeed.Id);
        var otherEnabledFeed = (await gistHandler.InsertTestFeedInfosAsync(1)).Single();
        var gistsFromOtherEnabledFeed = await gistHandler.InsertTestGistsAsync(5, otherEnabledFeed.Id);
        var disabledFeedIds = new[] { gistsFromDisabledFeed.First().FeedId, gistsFromOtherDisabledFeed.First().FeedId };
        var expectedGistsWithFeed = gistsFromEnabledFeed
            .Select(gist => GistWithFeed.FromGistAndFeed(gist, enabledFeed))
            .Concat(gistsFromOtherEnabledFeed.Select(gist => GistWithFeed.FromGistAndFeed(gist, otherEnabledFeed)))
            .ToList();
        var take = gistsFromDisabledFeed.Count
                   + gistsFromOtherDisabledFeed.Count
                   + gistsFromEnabledFeed.Count
                   + gistsFromOtherEnabledFeed.Count + 5;
        var gistsControllerHandler = CreateGistControllerHandler();

        var actualGistsWithFeed =
            await gistsControllerHandler.GetPreviousGistsWithFeedAsync(take, null, [], null, disabledFeedIds,
                CancellationToken.None);

        Assert.Equivalent(expectedGistsWithFeed, actualGistsWithFeed);
    }

    [Fact]
    public async Task GetGistByIdAsync_GistExists_CorrectGist()
    {
        var handler = CreateGistHandler();
        var testFeed = (await handler.InsertTestFeedInfosAsync(1)).Single();
        var testGist = (await handler.InsertTestGistsAsync(1, testFeed.Id)).Single();
        var expectedGist = GistWithFeed.FromGistAndFeed(testGist, testFeed);

        var actualGistWithFeed = await handler.GetGistWithFeedByIdAsync(expectedGist.Id, CancellationToken.None);

        Assert.Equivalent(expectedGist, actualGistWithFeed);
    }

    [Fact]
    public async Task GetGistByIdAsync_GistDoesNotExist_Null()
    {
        var handler = CreateGistHandler();

        var actual = await handler.GetGistWithFeedByIdAsync(1234566789, CancellationToken.None);

        Assert.Null(actual);
    }

    [Fact]
    public async Task GetAllFeedInfosAsync_NoFeedsExist_EmptyList()
    {
        var handler = CreateGistControllerHandler();

        var actual = await handler.GetAllFeedInfosAsync(CancellationToken.None);

        Assert.Empty(actual);
    }

    [Fact]
    public async Task GetAllFeedInfosAsync_FeedsExistInDb_ListWithAllFeeds()
    {
        var gistHandler = CreateGistHandler();
        var expected = await gistHandler.InsertTestFeedInfosAsync(10);
        var gistControllerHandler = CreateGistControllerHandler();

        var actual = await gistControllerHandler.GetAllFeedInfosAsync(CancellationToken.None);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task GetLatestRecapAsync_NoDailyRecapsExist_Null()
    {
        var handler = CreateGistControllerHandler();

        var actual = await handler.GetLatestRecapAsync(RecapType.Daily, CancellationToken.None);

        Assert.Null(actual);
    }

    [Fact]
    public async Task GetLatestRecapAsync_NoWeeklyRecapsExist_Null()
    {
        var handler = CreateGistControllerHandler();

        var actual = await handler.GetLatestRecapAsync(RecapType.Weekly, CancellationToken.None);

        Assert.Null(actual);
    }

    [Fact]
    public async Task GetLatestRecapAsync_DailyRecapsExist_LatestDailyRecap()
    {
        var dateTimeHandler = Substitute.For<IDateTimeHandler>();
        var recapHandler = CreateRecapHandler(dateTimeHandler);
        var expectedRecap = CreateTestRecap();
        var now = DateTime.UtcNow;
        dateTimeHandler.GetUtcNow().Returns(now.AddDays(-2));
        await recapHandler.InsertDailyRecapAsync(CreateTestRecap(), CancellationToken.None);
        dateTimeHandler.GetUtcNow().Returns(now);
        var recapId = await recapHandler.InsertDailyRecapAsync(expectedRecap, CancellationToken.None);
        dateTimeHandler.GetUtcNow().Returns(now.AddDays(-1));
        await recapHandler.InsertDailyRecapAsync(CreateTestRecap(), CancellationToken.None);
        dateTimeHandler.GetUtcNow().Returns(now);
        var truncatedNow = new DateTime(now.Ticks - now.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc);
        var expectedRecapString = JsonSerializer.Serialize(expectedRecap, SerializerDefaults.JsonOptions);
        var expected = new SerializedRecap(truncatedNow, expectedRecapString, recapId);
        var gistsControllerHandler = CreateGistControllerHandler(dateTimeHandler);

        var actual = await gistsControllerHandler.GetLatestRecapAsync(RecapType.Daily, CancellationToken.None);

        Assert.NotNull(actual);
        Assert.Equivalent(expected, actual);
    }

    [Fact]
    public async Task GetLatestRecapAsync_WeeklyRecapsExist_LatestWeeklyRecap()
    {
        var dateTimeHandler = Substitute.For<IDateTimeHandler>();
        var recapHandler = CreateRecapHandler(dateTimeHandler);
        var expectedRecap = CreateTestRecap();
        var now = DateTime.UtcNow;
        dateTimeHandler.GetUtcNow().Returns(now.AddDays(-2));
        await recapHandler.InsertWeeklyRecapAsync(CreateTestRecap(), CancellationToken.None);
        dateTimeHandler.GetUtcNow().Returns(now);
        var recapId = await recapHandler.InsertWeeklyRecapAsync(expectedRecap, CancellationToken.None);
        dateTimeHandler.GetUtcNow().Returns(now.AddDays(-1));
        await recapHandler.InsertWeeklyRecapAsync(CreateTestRecap(), CancellationToken.None);
        dateTimeHandler.GetUtcNow().Returns(now);
        var truncatedNow = new DateTime(now.Ticks - now.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc);
        var expectedRecapString = JsonSerializer.Serialize(expectedRecap, SerializerDefaults.JsonOptions);
        var expected = new SerializedRecap(truncatedNow, expectedRecapString, recapId);
        var gistsControllerHandler = CreateGistControllerHandler(dateTimeHandler);

        var actual = await gistsControllerHandler.GetLatestRecapAsync(RecapType.Weekly, CancellationToken.None);

        Assert.NotNull(actual);
        Assert.Equivalent(expected, actual);
    }

    [Fact]
    public async Task RegisterChatAsync_ChatIsNotRegistered_ChatIsRegistered()
    {
        var handler = CreateTelegramHandler();
        var chatId = _random.NextInt64();

        await handler.RegisterChatAsync(chatId, CancellationToken.None);

        await ChatAsserter.AssertChatIsInDbAsync(chatId);
    }

    [Fact]
    public async Task RegisterChatAsync_ChatIsAlreadyRegistered_MySqlException()
    {
        var handler = CreateTelegramHandler();
        var chatId = _random.NextInt64();
        await handler.RegisterChatAsync(chatId, CancellationToken.None);

        await Assert.ThrowsAsync<MySqlException>(() => handler.RegisterChatAsync(chatId, CancellationToken.None));
    }

    [Fact]
    public async Task RegisterChatAsync_NoGistsInDb_GistIdLastSentIsZero()
    {
        var handler = CreateTelegramHandler();
        var chatId = _random.NextInt64();

        await handler.RegisterChatAsync(chatId, CancellationToken.None);

        await ChatAsserter.AssertChatIsInDbAsync(chatId, 0);
    }

    [Fact]
    public async Task RegisterChatAsync_NoEnabledGistsInDb_GistIdLastSentIsZero()
    {
        var gistHandler = CreateGistHandler();
        var gists = await gistHandler.InsertTestGistsAsync(5);
        await Task.WhenAll(gists.Select(gist =>
            gistHandler.EnsureCorrectDisabledStateForGistAsync(gist.Id!.Value, true, CancellationToken.None)));
        var telegramHandler = CreateTelegramHandler();
        var chatId = _random.NextInt64();

        await telegramHandler.RegisterChatAsync(chatId, CancellationToken.None);

        await ChatAsserter.AssertChatIsInDbAsync(chatId, 0);
    }

    [Fact]
    public async Task RegisterChatAsync_EnabledGistsInDb_GistIdLastSentIsFromLatestGist()
    {
        var gistHandler = CreateGistHandler();
        var gists = await gistHandler.InsertTestGistsAsync(5);
        var expectedGistIdLastSent = gists.Select(gist => gist.Id!.Value).OrderDescending().First() - 5;
        var telegramHandler = CreateTelegramHandler();
        var chatId = _random.NextInt64();

        await telegramHandler.RegisterChatAsync(chatId, CancellationToken.None);

        await ChatAsserter.AssertChatIsInDbAsync(chatId, expectedGistIdLastSent);
    }

    [Fact]
    public async Task RegisterChatAsync_EnabledAndDisabledGistsInDb_GistIdLastSentIsFromLatestEnabledGist()
    {
        var gistHandler = CreateGistHandler();
        var enabledGists = await gistHandler.InsertTestGistsAsync(5);
        var expectedGistIdLastSent = enabledGists.Select(gist => gist.Id!.Value).OrderDescending().First() - 5;
        var disabledGists = await gistHandler.InsertTestGistsAsync(5);
        await Task.WhenAll(disabledGists.Select(gist =>
            gistHandler.EnsureCorrectDisabledStateForGistAsync(gist.Id!.Value, true, CancellationToken.None)));
        var telegramHandler = CreateTelegramHandler();
        var chatId = _random.NextInt64();

        await telegramHandler.RegisterChatAsync(chatId, CancellationToken.None);

        await ChatAsserter.AssertChatIsInDbAsync(chatId, expectedGistIdLastSent);
    }

    [Fact]
    public async Task DeregisterChatAsync_ChatIsNotRegistered_DatabaseOperationException()
    {
        var handler = CreateTelegramHandler();

        await Assert.ThrowsAsync<DatabaseOperationException>(() =>
            handler.DeregisterChatAsync(_random.NextInt64(), CancellationToken.None));
    }

    [Fact]
    public async Task DeregisterChatAsync_ChatIsRegistered_ChatIsDeregistered()
    {
        var handler = CreateTelegramHandler();
        var chatId = _random.NextInt64();
        await handler.RegisterChatAsync(chatId, CancellationToken.None);

        await handler.DeregisterChatAsync(chatId, CancellationToken.None);

        await ChatAsserter.AssertChatIsNotInDbAsync(chatId);
    }

    [Fact]
    public async Task IsChatRegisteredAsync_ChatIsNotRegistered_False()
    {
        var handler = CreateTelegramHandler();

        var actual = await handler.IsChatRegisteredAsync(_random.NextInt64(), CancellationToken.None);

        Assert.False(actual);
    }

    [Fact]
    public async Task IsChatRegisteredAsync_ChatIsNotRegisteredAfterDeregistered_False()
    {
        var handler = CreateTelegramHandler();
        var chatId = _random.NextInt64();
        await handler.RegisterChatAsync(chatId, CancellationToken.None);
        await handler.DeregisterChatAsync(chatId, CancellationToken.None);

        var actual = await handler.IsChatRegisteredAsync(chatId, CancellationToken.None);

        Assert.False(actual);
    }

    [Fact]
    public async Task IsChatRegisteredAsync_ChatIsRegistered_True()
    {
        var handler = CreateTelegramHandler();
        var chatId = _random.NextInt64();
        await handler.RegisterChatAsync(chatId, CancellationToken.None);

        var actual = await handler.IsChatRegisteredAsync(chatId, CancellationToken.None);

        Assert.True(actual);
    }

    [Fact]
    public async Task GetAllChatsAsync_NoChatsRegistered_EmptyList()
    {
        var handler = CreateTelegramHandler();

        var actual = await handler.GetAllChatsAsync(CancellationToken.None);

        Assert.Empty(actual);
    }

    [Fact]
    public async Task GetAllChatsAsync_RegisteredChatsWithoutGists_ListWithAllChatsAndCorrectGistIds()
    {
        var handler = CreateTelegramHandler();
        var expectedChats = await handler.InsertTestChatsAsync(10);

        var actual = await handler.GetAllChatsAsync(CancellationToken.None);

        Assert.Equivalent(expectedChats, actual);
    }

    [Fact]
    public async Task GetAllChatsAsync_RegisteredChatsWithGists_ListWithAllChatsAndCorrectGistIds()
    {
        var gistHandler = CreateGistHandler();
        await gistHandler.InsertTestGistsAsync(10);
        var telegramHandler = CreateTelegramHandler();
        var expectedChats = await telegramHandler.InsertTestChatsAsync(10);

        var actual = await telegramHandler.GetAllChatsAsync(CancellationToken.None);

        Assert.Equivalent(expectedChats, actual);
    }

    [Fact]
    public async Task GetNextFiveGistsAsync_NoGistsExist_EmptyList()
    {
        var handler = CreateTelegramHandler();

        var actual = await handler.GetNextFiveGistsWithFeedAsync(0, CancellationToken.None);

        Assert.Empty(actual);
    }

    [Fact]
    public async Task GetNextFiveGistsAsync_LessThanFiveGistsExist_EmptyList()
    {
        var gistHandler = CreateGistHandler();
        var testFeed = (await gistHandler.InsertTestFeedInfosAsync(1)).Single();
        var firstGist = (await gistHandler.InsertTestGistsAsync(1, testFeed.Id)).Single();
        var expected = (await gistHandler.InsertTestGistsAsync(3, testFeed.Id))
            .Select(g => GistWithFeed.FromGistAndFeed(g, testFeed)).OrderBy(g => g.Id).ToList();
        var telegramHandler = CreateTelegramHandler();

        var actual = await telegramHandler.GetNextFiveGistsWithFeedAsync(firstGist.Id!.Value, CancellationToken.None);

        Assert.Equivalent(expected, actual);
    }

    [Fact]
    public async Task GetNextFiveGistsAsync_MoreThanFiveGistsExist_EmptyList()
    {
        var gistHandler = CreateGistHandler();
        var testFeed = (await gistHandler.InsertTestFeedInfosAsync(1)).Single();
        var firstGist = (await gistHandler.InsertTestGistsAsync(1, testFeed.Id)).Single();
        var expected = (await gistHandler.InsertTestGistsAsync(6, testFeed.Id)).OrderBy(gist => gist.Id).Take(5)
            .Select(g => GistWithFeed.FromGistAndFeed(g, testFeed));
        var telegramHandler = CreateTelegramHandler();

        var actual = await telegramHandler.GetNextFiveGistsWithFeedAsync(firstGist.Id!.Value, CancellationToken.None);

        Assert.Equivalent(expected, actual);
    }

    [Fact]
    public async Task SetGistIdLastSentForChatAsync_ChatIsNotRegistered_DatabaseOperationException()
    {
        var handler = CreateTelegramHandler();

        await Assert.ThrowsAsync<DatabaseOperationException>(() =>
            handler.SetGistIdLastSentForChatAsync(_random.NextInt64(), _random.Next(), CancellationToken.None));
    }

    [Fact]
    public async Task SetGistIdLastSentForChatAsync_GistIdIsAlreadySetToThatValue_NothingChanged()
    {
        var gistHandler = CreateGistHandler();
        var gists = await gistHandler.InsertTestGistsAsync(6);
        var gistIdLastSent = gists.Select(gist => gist.Id!.Value).OrderDescending().First() - 5;
        var telegramHandler = CreateTelegramHandler();
        var chatId = _random.NextInt64();
        await telegramHandler.RegisterChatAsync(chatId, CancellationToken.None);

        await telegramHandler.SetGistIdLastSentForChatAsync(chatId, gistIdLastSent, CancellationToken.None);

        await ChatAsserter.AssertChatIsInDbAsync(chatId, gistIdLastSent);
    }

    [Fact]
    public async Task SetGistIdLastSentForChatAsync_NewGistId_ChatChanged()
    {
        var gistHandler = CreateGistHandler();
        await gistHandler.InsertTestGistsAsync(6);
        var telegramHandler = CreateTelegramHandler();
        var chatId = _random.NextInt64();
        await telegramHandler.RegisterChatAsync(chatId, CancellationToken.None);
        var gistIdLastSent = _random.Next();

        await telegramHandler.SetGistIdLastSentForChatAsync(chatId, gistIdLastSent, CancellationToken.None);

        await ChatAsserter.AssertChatIsInDbAsync(chatId, gistIdLastSent);
    }

    private MariaDbHandler CreateGistHandler(IDateTimeHandler? dateTimeHandler = null) =>
        CreateMariaDbHandler(_gistHandlerOptions, dateTimeHandler);

    private MariaDbHandler CreateRecapHandler(IDateTimeHandler? dateTimeHandler = null) =>
        CreateMariaDbHandler(_recapHandlerOptions, dateTimeHandler);

    private MariaDbHandler CreateCleanupHandler(IDateTimeHandler? dateTimeHandler = null) =>
        CreateMariaDbHandler(_cleanupHandlerOptions, dateTimeHandler);

    private MariaDbHandler CreateGistControllerHandler(IDateTimeHandler? dateTimeHandler = null) =>
        CreateMariaDbHandler(_gistControllerHandlerOptions, dateTimeHandler);

    private MariaDbHandler CreateTelegramHandler(IDateTimeHandler? dateTimeHandler = null) =>
        CreateMariaDbHandler(_telegramHandlerOptions, dateTimeHandler);

    private static MariaDbHandler
        CreateMariaDbHandler(MariaDbHandlerOptions options, IDateTimeHandler? dateTimeHandler) =>
        new(Options.Create(options), dateTimeHandler ?? new DateTimeHandler(), null);
}
