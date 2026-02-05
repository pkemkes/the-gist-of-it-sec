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
using TestUtilities;
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
        var feedInfoToUpdate = CreateTestFeedInfo(Language.De);
        var feedInfoId = await handler.InsertFeedInfoAsync(feedInfoToUpdate, CancellationToken.None);
        var expectedFeedInfo = feedInfoToUpdate with { Language = Language.En };

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
        await handler.InsertFeedInfoAsync(CreateTestFeedInfo(Language.De), CancellationToken.None);
        await handler.InsertFeedInfoAsync(CreateTestFeedInfo(Language.De), CancellationToken.None);
        var expectedFeedInfo = CreateTestFeedInfo(Language.De);
        var feedInfoId = await handler.InsertFeedInfoAsync(expectedFeedInfo, CancellationToken.None);
        await handler.InsertFeedInfoAsync(CreateTestFeedInfo(Language.De), CancellationToken.None);
        await handler.InsertFeedInfoAsync(CreateTestFeedInfo(Language.De), CancellationToken.None);

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
    public async Task InsertSummaryAsync_SummaryDoesNotExist_SummaryIsInsertedInDb()
    {
        var handler = CreateGistHandler();
        var feedInfo = CreateTestFeedInfo();
        var feedInfoId = await handler.InsertFeedInfoAsync(feedInfo, CancellationToken.None);
        var gist = CreateTestGist(feedInfoId);
        var gistId = await handler.InsertGistAsync(gist, CancellationToken.None);
        var summaryToInsert = CreateTestSummary(feedInfo.Language, false, gistId);

        await handler.InsertSummaryAsync(summaryToInsert, CancellationToken.None);

        await GistAsserter.AssertSummaryIsInDbAsync(summaryToInsert);
    }

    [Fact]
    public async Task InsertSummaryAsync_SummaryExistsAlready_ThrowsMySqlException()
    {
        var handler = CreateGistHandler();
        var feedInfo = CreateTestFeedInfo();
        var feedInfoId = await handler.InsertFeedInfoAsync(feedInfo, CancellationToken.None);
        var gist = CreateTestGist(feedInfoId);
        var gistId = await handler.InsertGistAsync(gist, CancellationToken.None);
        var summaryToInsert = CreateTestSummary(feedInfo.Language, false, gistId);
        await handler.InsertSummaryAsync(summaryToInsert, CancellationToken.None);

        await Assert.ThrowsAsync<MySqlException>(() =>
            handler.InsertSummaryAsync(summaryToInsert, CancellationToken.None));
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
    public async Task UpdateSummaryAsync_EverythingDifferentExceptGistId_InformationUpdated()
    {
        var handler = CreateGistHandler();
        var feedInfo = CreateTestFeedInfo();
        var feedInfoId = await handler.InsertFeedInfoAsync(feedInfo, CancellationToken.None);
        var gist = CreateTestGist(feedInfoId);
        var gistId = await handler.InsertGistAsync(gist, CancellationToken.None);
        var existingSummary = CreateTestSummary(feedInfo.Language, false, gistId);
        await handler.InsertSummaryAsync(existingSummary, CancellationToken.None);
        var summaryToUpdate = existingSummary with
        {
            Title = "updated title",
            SummaryText = "updated summary"
        };

        await using var handle = await handler.OpenTransactionAsync(CancellationToken.None);
        await handler.UpdateSummaryAsync(summaryToUpdate, handle, CancellationToken.None);
        await handler.CommitTransactionAsync(handle.Transaction, CancellationToken.None);

        await GistAsserter.AssertSummaryIsInDbAsync(summaryToUpdate);
    }

    [Fact]
    public async Task UpdateSummaryAsync_SummaryDoesNotExist_ThrowsDatabaseOperationException()
    {
        var handler = CreateGistHandler();
        var feedInfo = CreateTestFeedInfo();
        var feedInfoId = await handler.InsertFeedInfoAsync(feedInfo, CancellationToken.None);
        var gist = CreateTestGist(feedInfoId);
        var gistId = await handler.InsertGistAsync(gist, CancellationToken.None);
        var summaryToUpdate = CreateTestSummary(feedInfo.Language, false, gistId);

        await using var handle = await handler.OpenTransactionAsync(CancellationToken.None);

        await Assert.ThrowsAsync<DatabaseOperationException>(() =>
            handler.UpdateSummaryAsync(summaryToUpdate, handle, CancellationToken.None));
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

    [Theory]
    [InlineData(RecapType.Daily)]
    [InlineData(RecapType.Weekly)]
    public async Task RecapExistsAsync_NoRecapsExist_False(RecapType recapType)
    {
        var handler = CreateRecapHandler();

        var actual = recapType == RecapType.Daily
            ? await handler.DailyRecapExistsAsync(CancellationToken.None)
            : await handler.WeeklyRecapExistsAsync(CancellationToken.None);

        Assert.False(actual);
    }

    [Theory]
    [InlineData(RecapType.Daily)]
    [InlineData(RecapType.Weekly)]
    public async Task RecapExistsAsync_NoRecapOfTodayExists_False(RecapType recapType)
    {
        var baseCreated = _random.NextDateTime();
        var yesterday = new DateTime(baseCreated.Year, baseCreated.Month, baseCreated.Day, 12, 0, 0, DateTimeKind.Utc);
        var dateTimeHandlerMock = Substitute.For<IDateTimeHandler>();
        var handler = CreateRecapHandler(dateTimeHandlerMock);
        dateTimeHandlerMock.GetUtcNow().Returns(yesterday);
        await InsertTestRecapAsync(handler, recapType);
        var now = yesterday.AddDays(1);
        dateTimeHandlerMock.GetUtcNow().Returns(now);

        var actual = recapType == RecapType.Daily
            ? await handler.DailyRecapExistsAsync(CancellationToken.None)
            : await handler.WeeklyRecapExistsAsync(CancellationToken.None);

        Assert.False(actual);
    }

    [Theory]
    [InlineData(RecapType.Daily)]
    [InlineData(RecapType.Weekly)]
    public async Task RecapExistsAsync_OneRecapOfTodayExists_True(RecapType recapType)
    {
        var baseCreated = _random.NextDateTime();
        var yesterday = new DateTime(baseCreated.Year, baseCreated.Month, baseCreated.Day, 12, 0, 0, DateTimeKind.Utc);
        var dateTimeHandlerMock = Substitute.For<IDateTimeHandler>();
        var handler = CreateRecapHandler(dateTimeHandlerMock);
        dateTimeHandlerMock.GetUtcNow().Returns(yesterday);
        await InsertTestRecapAsync(handler, recapType);
        var today = yesterday.AddDays(1);
        dateTimeHandlerMock.GetUtcNow().Returns(today);
        await InsertTestRecapAsync(handler, recapType);
        var now = today.AddHours(6);
        dateTimeHandlerMock.GetUtcNow().Returns(now);

        var actual = recapType == RecapType.Daily
            ? await handler.DailyRecapExistsAsync(CancellationToken.None)
            : await handler.WeeklyRecapExistsAsync(CancellationToken.None);

        Assert.True(actual);
    }

    [Theory]
    [InlineData(RecapType.Daily)]
    [InlineData(RecapType.Weekly)]
    public async Task RecapExistsAsync_TwoRecapsOfTodayExist_DatabaseOperationException(RecapType recapType)
    {
        var baseCreated = _random.NextDateTime();
        var yesterday = new DateTime(baseCreated.Year, baseCreated.Month, baseCreated.Day, 12, 0, 0, DateTimeKind.Utc);
        var dateTimeHandlerMock = Substitute.For<IDateTimeHandler>();
        var handler = CreateRecapHandler(dateTimeHandlerMock);
        dateTimeHandlerMock.GetUtcNow().Returns(yesterday);
        await InsertTestRecapAsync(handler, recapType);
        var today = yesterday.AddDays(1);
        dateTimeHandlerMock.GetUtcNow().Returns(today);
        await InsertTestRecapAsync(handler, recapType);
        dateTimeHandlerMock.GetUtcNow().Returns(today.AddHours(1));
        await InsertTestRecapAsync(handler, recapType);
        var now = today.AddHours(6);
        dateTimeHandlerMock.GetUtcNow().Returns(now);

        await Assert.ThrowsAsync<DatabaseOperationException>(() =>
            recapType == RecapType.Daily
                ? handler.DailyRecapExistsAsync(CancellationToken.None)
                : handler.WeeklyRecapExistsAsync(CancellationToken.None));
    }

    private static Task<int> InsertTestRecapAsync(MariaDbHandler handler, RecapType recapType) =>
        recapType switch {
            RecapType.Daily => handler.InsertDailyRecapAsync(CreateTestRecap(), CancellationToken.None),
            RecapType.Weekly => handler.InsertWeeklyRecapAsync(CreateTestRecap(), CancellationToken.None),
            _ => throw new ArgumentOutOfRangeException(nameof(recapType), recapType, null)
        };

    [Fact]
    public async Task GetGistsOfLastDayAsync_NoGistsExist_EmptyList()
    {
        var dateTimeHandlerMock = Substitute.For<IDateTimeHandler>();
        dateTimeHandlerMock.GetUtcNow().Returns(_random.NextDateTime());
        var handler = CreateRecapHandler(dateTimeHandlerMock);

        var actual = await handler.GetConstructedGistsOfLastDayAsync(CancellationToken.None);

        Assert.Empty(actual);
    }

    [Fact]
    public async Task GetConstructedGistsOfLastDayAsync_GistsExist_ListWithOnlyNewerGist()
    {
        var testNow = _random.NextDateTime();
        var gistHandler = CreateGistHandler();
        var feedInfo = CreateTestFeedInfo();
        var feedInfoId = await gistHandler.InsertFeedInfoAsync(feedInfo, CancellationToken.None);
        var testGist = CreateTestGist(feedInfoId) with {
            Published = testNow.AddHours(-12),
            Updated = testNow.AddHours(-12)
        };
        testGist.Id = await gistHandler.InsertGistAsync(testGist, CancellationToken.None);
        var testSummaries = await gistHandler.InsertTestSummariesAsync(testGist.Id!.Value, feedInfo.Language);
        var testConstructedGist = ConstructedGist.FromGistFeedAndSummary(testGist, feedInfo, testSummaries.First());
        var olderTestGist = CreateTestGist(feedInfoId) with {
            Published = testNow.AddHours(-36),
            Updated = testNow.AddHours(-36)
        };
        olderTestGist.Id = await gistHandler.InsertGistAsync(olderTestGist, CancellationToken.None);
        await gistHandler.InsertTestSummariesAsync(olderTestGist.Id!.Value, feedInfo.Language);
        var dateTimeHandlerMock = Substitute.For<IDateTimeHandler>();
        dateTimeHandlerMock.GetUtcNow().Returns(testNow);
        var recapHandler = CreateRecapHandler(dateTimeHandlerMock);

        var actual = await recapHandler.GetConstructedGistsOfLastDayAsync(CancellationToken.None);

        Assert.Single(actual);
        Assert.Equivalent(testConstructedGist, actual.Single());
    }

    [Fact]
    public async Task GetConstructedGistsOfLastDayAsync_SomeGistsAreSponsoredContent_OnlyNotSponsoredGists()
    {
        var testNow = _random.NextDateTime();
        var gistHandler = CreateGistHandler();
        var feedInfo = CreateTestFeedInfo();
        var feedInfoId = await gistHandler.InsertFeedInfoAsync(feedInfo, CancellationToken.None);
        var nonSponsoredGist = CreateTestGist(feedInfoId) with {
            Published = testNow.AddHours(-12),
            Updated = testNow.AddHours(-12),
            IsSponsoredContent = false
        };
        nonSponsoredGist.Id = await gistHandler.InsertGistAsync(nonSponsoredGist, CancellationToken.None);
        var nonSponsoredSummaries =
            await gistHandler.InsertTestSummariesAsync(nonSponsoredGist.Id!.Value, feedInfo.Language);
        var nonSponsoredConstructedGist =
            ConstructedGist.FromGistFeedAndSummary(nonSponsoredGist, feedInfo, nonSponsoredSummaries.First());
        var sponsoredGist = CreateTestGist(feedInfoId) with {
            Published = testNow.AddHours(-10),
            Updated = testNow.AddHours(-10),
            IsSponsoredContent = true
        };
        sponsoredGist.Id = await gistHandler.InsertGistAsync(sponsoredGist, CancellationToken.None);
        await gistHandler.InsertTestSummariesAsync(sponsoredGist.Id!.Value, feedInfo.Language);
        var dateTimeHandlerMock = Substitute.For<IDateTimeHandler>();
        dateTimeHandlerMock.GetUtcNow().Returns(testNow);
        var recapHandler = CreateRecapHandler(dateTimeHandlerMock);

        var actual = await recapHandler.GetConstructedGistsOfLastDayAsync(CancellationToken.None);

        Assert.Single(actual);
        Assert.Equivalent(nonSponsoredConstructedGist, actual.Single());
    }

    [Fact]
    public async Task GetConstructedGistsOfLastWeekAsync_NoGistsExist_EmptyList()
    {
        var dateTimeHandlerMock = Substitute.For<IDateTimeHandler>();
        dateTimeHandlerMock.GetUtcNow().Returns(_random.NextDateTime());
        var handler = CreateRecapHandler(dateTimeHandlerMock);

        var actual = await handler.GetConstructedGistsOfLastWeekAsync(CancellationToken.None);

        Assert.Empty(actual);
    }

    [Fact]
    public async Task GetConstructedGistsOfLastWeekAsync_GistsExist_ListWithOnlyNewerGist()
    {
        var testNow = _random.NextDateTime();
        var gistHandler = CreateGistHandler();
        var feedInfo = CreateTestFeedInfo();
        var feedInfoId = await gistHandler.InsertFeedInfoAsync(feedInfo, CancellationToken.None);
        var testGist = CreateTestGist(feedInfoId) with {
            Published = testNow.AddDays(-3),
            Updated = testNow.AddDays(-3)
        };
        testGist.Id = await gistHandler.InsertGistAsync(testGist, CancellationToken.None);
        var testSummaries = await gistHandler.InsertTestSummariesAsync(testGist.Id!.Value, feedInfo.Language);
        var testConstructedGist = ConstructedGist.FromGistFeedAndSummary(testGist, feedInfo, testSummaries.First());
        var olderTestGist = CreateTestGist(feedInfoId) with {
            Published = testNow.AddDays(-10),
            Updated = testNow.AddDays(-10)
        };
        olderTestGist.Id = await gistHandler.InsertGistAsync(olderTestGist, CancellationToken.None);
        await gistHandler.InsertTestSummariesAsync(olderTestGist.Id!.Value, feedInfo.Language);
        var dateTimeHandlerMock = Substitute.For<IDateTimeHandler>();
        dateTimeHandlerMock.GetUtcNow().Returns(testNow);
        var recapHandler = CreateRecapHandler(dateTimeHandlerMock);

        var actual = await recapHandler.GetConstructedGistsOfLastWeekAsync(CancellationToken.None);

        Assert.Single(actual);
        Assert.Equivalent(testConstructedGist, actual.Single());
    }

    [Fact]
    public async Task GetConstructedGistsOfLastWeekAsync_SomeGistsAreSponsoredContent_OnlyNotSponsoredGists()
    {
        var testNow = _random.NextDateTime();
        var gistHandler = CreateGistHandler();
        var feedInfo = CreateTestFeedInfo();
        var feedInfoId = await gistHandler.InsertFeedInfoAsync(feedInfo, CancellationToken.None);
        var nonSponsoredGist = CreateTestGist(feedInfoId) with {
            Published = testNow.AddDays(-3),
            Updated = testNow.AddDays(-3),
            IsSponsoredContent = false
        };
        nonSponsoredGist.Id = await gistHandler.InsertGistAsync(nonSponsoredGist, CancellationToken.None);
        var nonSponsoredSummaries =
            await gistHandler.InsertTestSummariesAsync(nonSponsoredGist.Id!.Value, feedInfo.Language);
        var nonSponsoredConstructedGist =
            ConstructedGist.FromGistFeedAndSummary(nonSponsoredGist, feedInfo, nonSponsoredSummaries.First());
        var sponsoredGist = CreateTestGist(feedInfoId) with {
            Published = testNow.AddDays(-2),
            Updated = testNow.AddDays(-2),
            IsSponsoredContent = true
        };
        sponsoredGist.Id = await gistHandler.InsertGistAsync(sponsoredGist, CancellationToken.None);
        await gistHandler.InsertTestSummariesAsync(sponsoredGist.Id!.Value, feedInfo.Language);
        var dateTimeHandlerMock = Substitute.For<IDateTimeHandler>();
        dateTimeHandlerMock.GetUtcNow().Returns(testNow);
        var recapHandler = CreateRecapHandler(dateTimeHandlerMock);

        var actual = await recapHandler.GetConstructedGistsOfLastWeekAsync(CancellationToken.None);

        Assert.Single(actual);
        Assert.Equivalent(nonSponsoredConstructedGist, actual.Single());
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
    public async Task GetPreviousConstructedGistsAsync_NoGists_EmptyList()
    {
        var handler = CreateGistControllerHandler();

        var gists = await handler.GetPreviousConstructedGistsAsync(10, null, [], null, [], LanguageMode.Original,
            null, CancellationToken.None);

        Assert.Empty(gists);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(8)]
    public async Task GetPreviousConstructedGistsAsync_LessGistsThanTake_AllGists(int gistCount)
    {
        var gistHandler = CreateGistHandler();
        const LanguageMode languageMode = LanguageMode.Original;
        const bool isSponsoredContent = false;
        var expectedConstructedGists =
            await gistHandler.InsertTestConstructedGistsAsync(gistCount, languageMode: languageMode,
                isSponsoredContent: isSponsoredContent);
        var gistsControllerHandler = CreateGistControllerHandler();

        var actualConstructedGists =
            await gistsControllerHandler.GetPreviousConstructedGistsAsync(gistCount + 5, null, [], null, [],
                languageMode, isSponsoredContent, CancellationToken.None);

        Assert.Equivalent(expectedConstructedGists, actualConstructedGists);
    }

    [Fact]
    public async Task GetPreviousConstructedGistsAsync_MoreGistsThanTake_AsManyGistsAsExpected()
    {
        var gistHandler = CreateGistHandler();
        const int take = 5;
        const LanguageMode languageMode = LanguageMode.Original;
        const bool isSponsoredContent = false;
        var constructedGists = await gistHandler.InsertTestConstructedGistsAsync(take + 5, languageMode: languageMode,
            isSponsoredContent: isSponsoredContent);
        var expectedConstructedGists = constructedGists.Take(take).ToList();
        var gistsControllerHandler = CreateGistControllerHandler();

        var actualConstructedGists =
            await gistsControllerHandler.GetPreviousConstructedGistsAsync(take, null, [], null, [],
                languageMode, isSponsoredContent, CancellationToken.None);

        Assert.Equivalent(expectedConstructedGists, actualConstructedGists);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(8)]
    public async Task GetPreviousConstructedGistsAsync_LastGistGivenId_GistsAfterLastGistId(int take)
    {
        var gistHandler = CreateGistHandler();
        const LanguageMode languageMode = LanguageMode.Original;
        const bool isSponsoredContent = false;
        var testConstructedGists = await gistHandler.InsertTestConstructedGistsAsync(10, languageMode: languageMode,
            isSponsoredContent: isSponsoredContent);
        var firstHalfOfGists = testConstructedGists.Skip(5).ToList();
        var lastGistId = testConstructedGists[4].Id;
        var expectedConstructedGists = firstHalfOfGists.Take(take).ToList();
        var gistsControllerHandler = CreateGistControllerHandler();

        var actualGistsWithFeed =
            await gistsControllerHandler.GetPreviousConstructedGistsAsync(take, lastGistId, [], null, [],
                LanguageMode.Original, isSponsoredContent, CancellationToken.None);

        Assert.Equivalent(expectedConstructedGists, actualGistsWithFeed);
    }

    [Fact]
    public async Task GetPreviousConstructedGistsAsync_QuerySpecificTags_GistsWithAllTags()
    {
        var tags = new[] { "tag1", "tag2", "tag3" };
        var gistHandler = CreateGistHandler();
        var testFeed = (await gistHandler.InsertTestFeedInfosAsync(Language.De, 1)).Single();
        var gistWithoutExpectedTags = CreateTestGist(testFeed.Id);
        var gistWithOnlyOneExpectedTag = CreateTestGist(testFeed.Id) with { Tags = tags.First() };
        var gistWithExpectedTags = CreateTestGist(testFeed.Id) with { Tags = string.Join(";;", tags) };
        var gistWithExpectedAndOtherTags = CreateTestGist(testFeed.Id) with {
            Tags = string.Join(";;", tags.Concat(_random.NextArrayOfStrings(3)))
        };
        gistWithoutExpectedTags.Id = await gistHandler.InsertGistAsync(gistWithoutExpectedTags, CancellationToken.None);
        await gistHandler.InsertTestSummariesAsync(gistWithoutExpectedTags.Id!.Value, testFeed.Language);
        gistWithOnlyOneExpectedTag.Id =
            await gistHandler.InsertGistAsync(gistWithOnlyOneExpectedTag, CancellationToken.None);
        await gistHandler.InsertTestSummariesAsync(gistWithOnlyOneExpectedTag.Id!.Value, testFeed.Language);
        gistWithExpectedTags.Id = await gistHandler.InsertGistAsync(gistWithExpectedTags, CancellationToken.None);
        var summaryOfGistWithExpectedTags =
            (await gistHandler.InsertTestSummariesAsync(gistWithExpectedTags.Id!.Value, testFeed.Language)).First();
        gistWithExpectedAndOtherTags.Id =
            await gistHandler.InsertGistAsync(gistWithExpectedAndOtherTags, CancellationToken.None);
        var summaryOfGistWithExpectedAndOtherTags =
            (await gistHandler.InsertTestSummariesAsync(gistWithExpectedAndOtherTags.Id!.Value, testFeed.Language))
            .First();
        var expectedGistsWithFeed = new List<ConstructedGist>
        {
            ConstructedGist.FromGistFeedAndSummary(gistWithExpectedTags, testFeed, summaryOfGistWithExpectedTags),
            ConstructedGist.FromGistFeedAndSummary(gistWithExpectedAndOtherTags, testFeed,
                summaryOfGistWithExpectedAndOtherTags)
        };
        var gistsControllerHandler = CreateGistControllerHandler();

        var actualGistsWithFeed =
            await gistsControllerHandler.GetPreviousConstructedGistsAsync(10, null, tags, null, [],
                LanguageMode.Original, null, CancellationToken.None);

        Assert.Equivalent(expectedGistsWithFeed, actualGistsWithFeed);
    }

    [Fact]
    public async Task GetPreviousConstructedGistsAsync_QueryWordsFromTitleAndSummary_GistsWithAllWords()
    {
        var words = new[] { "word1", "word2", "word3" };
        var gistHandler = CreateGistHandler();
        var testFeed = (await gistHandler.InsertTestFeedInfosAsync(Language.De, 1)).Single();
        var gistWithoutExpectedWords = await gistHandler.InsertTestGistAsync(testFeed.Id);
        await gistHandler.InsertTestSummariesAsync(gistWithoutExpectedWords.Id!.Value, testFeed.Language);
        var gistWithOnlyOneExpectedWordInTitle = await gistHandler.InsertTestGistAsync(testFeed.Id);
        var summaryOfGistWithOnlyOneExpectedWordInTitle =
            CreateTestSummary(testFeed.Language, false, gistWithOnlyOneExpectedWordInTitle.Id) with {
            Title = $"This is a {words.First()} title"
        };
        await gistHandler.InsertSummaryAsync(summaryOfGistWithOnlyOneExpectedWordInTitle, CancellationToken.None);
        var gistWithAllExpectedWordsInTitle = await gistHandler.InsertTestGistAsync(testFeed.Id);
        var summaryOfGistWithAllExpectedWordsInTitle =
            CreateTestSummary(testFeed.Language, false, gistWithAllExpectedWordsInTitle.Id) with {
            Title = $"This is a {words[0]}someextratext and {words[1]}{words[2]} title"
        };
        await gistHandler.InsertSummaryAsync(summaryOfGistWithAllExpectedWordsInTitle, CancellationToken.None);
        var gistWithAllExpectedWordsInSummary = await gistHandler.InsertTestGistAsync(testFeed.Id);
        var summaryOfGistWithAllExpectedWordsInSummary =
            CreateTestSummary(testFeed.Language, false, gistWithAllExpectedWordsInSummary.Id) with {
            SummaryText = $"This is a {words[0]}someextratext and {words[1]}{words[2]} summary"
        };
        await gistHandler.InsertSummaryAsync(summaryOfGistWithAllExpectedWordsInSummary, CancellationToken.None);
        var gistWithAllExpectedWords = await gistHandler.InsertTestGistAsync(testFeed.Id);
        var summaryOfGistWithAllExpectedWords =
            CreateTestSummary(testFeed.Language, false, gistWithAllExpectedWords.Id) with {
            Title = $"This is a {words[0]}someextratext title",
            SummaryText = $"This is a {words[1]}{words[2]} summary"
        };
        await gistHandler.InsertSummaryAsync(summaryOfGistWithAllExpectedWords, CancellationToken.None);
        var expectedConstructedGists = new List<ConstructedGist>
        {
            ConstructedGist.FromGistFeedAndSummary(gistWithAllExpectedWords, testFeed, summaryOfGistWithAllExpectedWords),
            ConstructedGist.FromGistFeedAndSummary(gistWithAllExpectedWordsInTitle, testFeed, summaryOfGistWithAllExpectedWordsInTitle),
            ConstructedGist.FromGistFeedAndSummary(gistWithAllExpectedWordsInSummary, testFeed, summaryOfGistWithAllExpectedWordsInSummary)
        };
        var searchQuery = string.Join(' ', words);
        var gistsControllerHandler = CreateGistControllerHandler();

        var actualGistsWithFeed =
            await gistsControllerHandler.GetPreviousConstructedGistsAsync(10, null, [], searchQuery, [],
                LanguageMode.Original, null, CancellationToken.None);

        Assert.Equivalent(expectedConstructedGists, actualGistsWithFeed);
    }

    [Fact]
    public async Task GetPreviousConstructedGistsAsync_GistsFromDisabledFeedInDb_OnlyGistsFromEnabledFeeds()
    {
        var gistHandler = CreateGistHandler();
        var disabledFeed = (await gistHandler.InsertTestFeedInfosAsync(Language.De, 1)).Single();
        var gistsFromDisabledFeed = await gistHandler.InsertTestConstructedGistsAsync(5, disabledFeed);
        var otherDisabledFeed = (await gistHandler.InsertTestFeedInfosAsync(Language.En, 1)).Single();
        var gistsFromOtherDisabledFeed = await gistHandler.InsertTestConstructedGistsAsync(5, otherDisabledFeed);
        var enabledFeed = (await gistHandler.InsertTestFeedInfosAsync(Language.De, 1)).Single();
        var gistsFromEnabledFeed = await gistHandler.InsertTestConstructedGistsAsync(5, enabledFeed);
        var otherEnabledFeed = (await gistHandler.InsertTestFeedInfosAsync(Language.En, 1)).Single();
        var gistsFromOtherEnabledFeed = await gistHandler.InsertTestConstructedGistsAsync(5, otherEnabledFeed);
        var expectedGistsWithFeed = gistsFromEnabledFeed.Concat(gistsFromOtherEnabledFeed).ToList();
        var take = gistsFromDisabledFeed.Count
                   + gistsFromOtherDisabledFeed.Count
                   + gistsFromEnabledFeed.Count
                   + gistsFromOtherEnabledFeed.Count + 5;
        var gistsControllerHandler = CreateGistControllerHandler();

        var actualGistsWithFeed =
            await gistsControllerHandler.GetPreviousConstructedGistsAsync(take, null, [], null,
                [disabledFeed.Id!.Value, otherDisabledFeed.Id!.Value], LanguageMode.Original, null,
                CancellationToken.None);

        Assert.Equivalent(expectedGistsWithFeed, actualGistsWithFeed);
    }

    [Fact]
    public async Task GetPreviousConstructedGistsAsync_SomeGistsAreSponsoredContentButExcludedInQuery_OnlyNotSponsoredGists()
    {
        var gistHandler = CreateGistHandler();
        const LanguageMode languageMode = LanguageMode.Original;
        var sponsoredConstructedGists =
            await gistHandler.InsertTestConstructedGistsAsync(5, languageMode: languageMode, isSponsoredContent: true);
        var notSponsoredConstructedGists =
            await gistHandler.InsertTestConstructedGistsAsync(5, languageMode: languageMode, isSponsoredContent: false);
        var take = sponsoredConstructedGists.Count + notSponsoredConstructedGists.Count;
        var gistsControllerHandler = CreateGistControllerHandler();

        var actualGistsWithFeed =
            await gistsControllerHandler.GetPreviousConstructedGistsAsync(take, null, [], null, [],
                LanguageMode.Original, false, CancellationToken.None);

        Assert.Equivalent(notSponsoredConstructedGists, actualGistsWithFeed);
    }

    [Fact]
    public async Task GetPreviousConstructedGistsAsync_SomeGistsAreSponsoredContentAndIncludedInQuery_AllGists()
    {
        var gistHandler = CreateGistHandler();
        const LanguageMode languageMode = LanguageMode.Original;
        var sponsoredConstructedGists =
            await gistHandler.InsertTestConstructedGistsAsync(5, languageMode: languageMode, isSponsoredContent: true);
        var notSponsoredConstructedGists =
            await gistHandler.InsertTestConstructedGistsAsync(5, languageMode: languageMode, isSponsoredContent: false);
        var take = sponsoredConstructedGists.Count + notSponsoredConstructedGists.Count;
        var expectedConstructedGists = notSponsoredConstructedGists.Concat(sponsoredConstructedGists).ToList();
        var gistsControllerHandler = CreateGistControllerHandler();

        var actualGistsWithFeed =
            await gistsControllerHandler.GetPreviousConstructedGistsAsync(take, null, [], null, [],
                LanguageMode.Original, true, CancellationToken.None);

        Assert.Equivalent(expectedConstructedGists, actualGistsWithFeed);
    }

    [Fact]
    public async Task GetGistByIdAsync_GistExists_CorrectGist()
    {
        var handler = CreateGistHandler();
        var expectedGist = (await handler.InsertTestConstructedGistsAsync(1)).Single();

        var actualGistWithFeed =
            await handler.GetConstructedGistByIdAsync(expectedGist.Id, LanguageMode.Original, CancellationToken.None);

        Assert.Equivalent(expectedGist, actualGistWithFeed);
    }

    [Fact]
    public async Task GetGistByIdAsync_GistDoesNotExist_Null()
    {
        var handler = CreateGistHandler();

        var actual =
            await handler.GetConstructedGistByIdAsync(1234566789, LanguageMode.Original, CancellationToken.None);

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
        var germanFeeds = await gistHandler.InsertTestFeedInfosAsync(Language.De, 5);
        var englishFeeds = await gistHandler.InsertTestFeedInfosAsync(Language.En, 5);
        var expected = germanFeeds.Concat(englishFeeds).ToList();
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
        var expectedRecapStringDe =
            JsonSerializer.Serialize(expectedRecap.RecapSectionsGerman, SerializerDefaults.JsonOptions);
        var expectedRecapStringEn =
            JsonSerializer.Serialize(expectedRecap.RecapSectionsEnglish, SerializerDefaults.JsonOptions);
        var expected = new SerializedRecap(truncatedNow, expectedRecapStringEn, expectedRecapStringDe, recapId);
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
        var expectedRecapStringDe =
            JsonSerializer.Serialize(expectedRecap.RecapSectionsGerman, SerializerDefaults.JsonOptions);
        var expectedRecapStringEn =
            JsonSerializer.Serialize(expectedRecap.RecapSectionsEnglish, SerializerDefaults.JsonOptions);
        var expected = new SerializedRecap(truncatedNow, expectedRecapStringEn, expectedRecapStringDe, recapId);
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
        var gists = await gistHandler.InsertTestConstructedGistsAsync(10);
        var expectedGistIdLastSent = gists.Select(gist => gist.Id).OrderDescending().First() - 5;
        var telegramHandler = CreateTelegramHandler();
        var chatId = _random.NextInt64();

        await telegramHandler.RegisterChatAsync(chatId, CancellationToken.None);

        await ChatAsserter.AssertChatIsInDbAsync(chatId, expectedGistIdLastSent);
    }

    [Fact]
    public async Task RegisterChatAsync_EnabledAndDisabledGistsInDb_GistIdLastSentIsFromLatestEnabledGist()
    {
        var gistHandler = CreateGistHandler();
        var enabledGists = await gistHandler.InsertTestConstructedGistsAsync(10);
        var expectedGistIdLastSent = enabledGists.Select(gist => gist.Id).OrderDescending().First() - 5;
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
    public async Task GetNextFiveConstructedGistsAsync_NoGistsExist_EmptyList()
    {
        var handler = CreateTelegramHandler();

        var actual = await handler.GetNextFiveConstructedGistsAsync(0, LanguageMode.Original, CancellationToken.None);

        Assert.Empty(actual);
    }

    [Fact]
    public async Task GetNextFiveConstructedGistsAsync_LessThanFiveGistsExist_LessThanFiveGists()
    {
        var gistHandler = CreateGistHandler();
        const LanguageMode languageMode = LanguageMode.Original;
        var testGists = await gistHandler.InsertTestConstructedGistsAsync(4, languageMode: languageMode);
        testGists.Reverse();  // need to reverse to get gists in ascending order by ID
        var previousGist = testGists.First();
        var expected = testGists.Skip(1).ToList();
        var telegramHandler = CreateTelegramHandler();

        var actual =
            await telegramHandler.GetNextFiveConstructedGistsAsync(previousGist.Id, languageMode,
                CancellationToken.None);

        Assert.Equivalent(expected, actual);
    }

    [Fact]
    public async Task GetNextFiveConstructedGistsAsync_MoreThanFiveGistsExist_NextFiveGists()
    {
        var gistHandler = CreateGistHandler();
        const LanguageMode languageMode = LanguageMode.Original;
        var testGists = await gistHandler.InsertTestConstructedGistsAsync(7, languageMode: languageMode);
        testGists.Reverse();  // need to reverse to get gists in ascending order by ID
        var previousGist = testGists.First();
        var expected = testGists.Skip(1).Take(5).ToList();
        var telegramHandler = CreateTelegramHandler();

        var actual =
            await telegramHandler.GetNextFiveConstructedGistsAsync(previousGist.Id, languageMode, CancellationToken.None);

        Assert.Equivalent(expected, actual);
    }

    [Fact]
    public async Task GetNextFiveConstructedGistsAsync_SomeGistsAreSponsoredContent_OnlyNonSponsoredGists()
    {
        var gistHandler = CreateGistHandler();
        const LanguageMode languageMode = LanguageMode.Original;
        var nonSponsoredGists =
            await gistHandler.InsertTestConstructedGistsAsync(2, languageMode: languageMode, isSponsoredContent: false);
        var sponsoredGists =
            await gistHandler.InsertTestConstructedGistsAsync(2, languageMode: languageMode, isSponsoredContent: true);
        var moreNonSponsoredGists =
            await gistHandler.InsertTestConstructedGistsAsync(2, languageMode: languageMode, isSponsoredContent: false);
        // need to reverse to get gists in ascending order by ID
        nonSponsoredGists.Reverse();
        sponsoredGists.Reverse();
        moreNonSponsoredGists.Reverse();
        var previousGist = nonSponsoredGists.First();
        var expected = nonSponsoredGists.Skip(1).Concat(moreNonSponsoredGists).ToList();
        var telegramHandler = CreateTelegramHandler();

        var actual =
            await telegramHandler.GetNextFiveConstructedGistsAsync(previousGist.Id, languageMode, CancellationToken.None);

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

    [Fact]
    public async Task InsertDisabledGistAsync_GistDoesNotExist_DisabledGistIsInsertedInDb()
    {
        var handler = CreateGistHandler();
        var feedInfo = CreateTestFeedInfo();
        var feedInfoId = await handler.InsertFeedInfoAsync(feedInfo, CancellationToken.None);
        var rssEntry = CreateTestEntry(feedInfoId);
        var disabledGistToInsert = new DisabledGist(rssEntry);

        await handler.InsertDisabledGistAsync(disabledGistToInsert, CancellationToken.None);

        await GistAsserter.AssertDisabledGistIsInDbAsync(disabledGistToInsert);
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
