using GistBackend.Handler;
using GistBackend.Handler.MariaDbHandler;
using GistBackend.IntegrationTest.Utils;
using GistBackend.Types;
using Microsoft.Extensions.Options;
using NSubstitute;
using static GistBackend.IntegrationTest.Utils.TestData;

namespace GistBackend.IntegrationTest;

[Collection(nameof(TestsWithoutParallelizationCollection))]
public class MariaDbHandlerConsecutiveTests : IClassFixture<MariaDbFixture>
{
    private readonly Random _random = new();
    private readonly MariaDbHandlerOptions _gistHandlerOptions;
    private readonly MariaDbHandlerOptions _recapHandlerOptions;
    private readonly MariaDbHandlerOptions _gistControllerHandlerOptions;

    public MariaDbHandlerConsecutiveTests(MariaDbFixture fixture)
    {
        _gistHandlerOptions = new MariaDbHandlerOptions(
            fixture.Hostname,
            MariaDbFixture.GistServiceDbUsername,
            MariaDbFixture.GistServiceDbPassword,
            fixture.ExposedPort
        );
        _recapHandlerOptions = new MariaDbHandlerOptions(
            fixture.Hostname,
            MariaDbFixture.RecapServiceDbUsername,
            MariaDbFixture.RecapServiceDbPassword,
            fixture.ExposedPort
        );
        _gistControllerHandlerOptions = new MariaDbHandlerOptions(
            fixture.Hostname,
            MariaDbFixture.GistsControllerDbUsername,
            MariaDbFixture.GistsControllerDbPassword,
            fixture.ExposedPort
        );
        fixture.ClearDatabaseAsync().GetAwaiter().GetResult();
    }

    [Fact]
    public async Task GetPreviousGistsAsync_NoGists_ReturnsEmptyList()
    {
        var handler = CreateGistControllerHandler();

        var gists = await handler.GetPreviousGistsAsync(10, null, [], null, [], CancellationToken.None);

        Assert.Empty(gists);
    }

    [Fact]
    public async Task GetAllGistsAsync_NoGistsExist_EmptyList()
    {
        var handler = CreateGistControllerHandler();

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
        var gistsControllerHandler = CreateGistControllerHandler();

        var actual = await gistsControllerHandler.GetAllGistsAsync(CancellationToken.None);

        Assert.Equivalent(expected, actual);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(8)]
    public async Task GetPreviousGistsAsync_LessGistsThanTake_ReturnsAllGists(int gistCount)
    {
        var gistHandler = CreateGistHandler();
        var expectedGists = await gistHandler.InsertTestGistsAsync(gistCount);
        var gistsControllerHandler = CreateGistControllerHandler();

        var actualGists =
            await gistsControllerHandler.GetPreviousGistsAsync(gistCount + 5, null, [], null, [],
                CancellationToken.None);

        Assert.Equivalent(expectedGists, actualGists);
    }

    [Fact]
    public async Task GetPreviousGistsAsync_MoreGistsThanTake_ReturnsAsManyGistsAsExpected()
    {
        var gistHandler = CreateGistHandler();
        const int take = 5;
        var testGists = await gistHandler.InsertTestGistsAsync(take+5);
        var expectedGists = testGists.Take(take).ToList();
        var gistsControllerHandler = CreateGistControllerHandler();

        var actualGists =
            await gistsControllerHandler.GetPreviousGistsAsync(take, null, [], null, [], CancellationToken.None);

        Assert.Equivalent(expectedGists, actualGists);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(8)]
    public async Task GetPreviousGistsAsync_LastGistGivenId_ReturnsGistsAfterLastGistId(int take)
    {
        var gistHandler = CreateGistHandler();
        var testGists = await gistHandler.InsertTestGistsAsync(10);
        var firstHalfOfGists = testGists.Skip(5).ToList();
        var lastGistId = testGists[4].Id;
        var expectedGists = firstHalfOfGists.Take(take).ToList();
        var gistsControllerHandler = CreateGistControllerHandler();

        var actualGists =
            await gistsControllerHandler.GetPreviousGistsAsync(take, lastGistId, [], null, [], CancellationToken.None);

        Assert.Equivalent(expectedGists, actualGists);
    }

    [Fact]
    public async Task GetPreviousGistsAsync_QuerySpecificTags_ReturnsGistsWithAllTags()
    {
        var tags = new[] { "tag1", "tag2", "tag3" };
        var gistHandler = CreateGistHandler();
        var feedId = await gistHandler.InsertFeedInfoAsync(CreateTestFeedInfo(), CancellationToken.None);
        var gistWithoutExpectedTags = CreateTestGist(feedId);
        var gistWithOnlyOneExpectedTag = CreateTestGist(feedId) with {
            Tags = string.Join(";;", tags.First())
        };
        var gistWithExpectedTags = CreateTestGist(feedId) with { Tags = string.Join(";;", tags) };
        var gistWithExpectedAndOtherTags = CreateTestGist(feedId) with {
            Tags = string.Join(";;", tags.Concat(_random.NextArrayOfStrings(3)))
        };
        await gistHandler.InsertGistAsync(gistWithoutExpectedTags, CancellationToken.None);
        await gistHandler.InsertGistAsync(gistWithOnlyOneExpectedTag, CancellationToken.None);
        gistWithExpectedTags.Id = await gistHandler.InsertGistAsync(gistWithExpectedTags, CancellationToken.None);
        gistWithExpectedAndOtherTags.Id =
            await gistHandler.InsertGistAsync(gistWithExpectedAndOtherTags, CancellationToken.None);
        var expectedGists = new List<Gist>
            { gistWithExpectedTags, gistWithExpectedAndOtherTags };
        var gistsControllerHandler = CreateGistControllerHandler();

        var actualGists =
            await gistsControllerHandler.GetPreviousGistsAsync(10, null, tags, null, [], CancellationToken.None);

        Assert.Equivalent(expectedGists, actualGists);
    }

    [Fact]
    public async Task GetPreviousGistsAsync_QueryWordsFromTitleAndSummary_ReturnsGistsWithAllWords()
    {
        var words = new[] { "word1", "word2", "word3" };
        var gistHandler = CreateGistHandler();
        var feedId = await gistHandler.InsertFeedInfoAsync(CreateTestFeedInfo(), CancellationToken.None);
        var gistWithoutExpectedWords = CreateTestGist(feedId);
        var gistWithOnlyOneExpectedWordInTitle = CreateTestGist(feedId) with {
            Title = $"This is a {words.First()} title"
        };
        var gistWithAllExpectedWordsInTitle = CreateTestGist(feedId) with {
            Title = $"This is a {words[0]}someextratext and {words[1]}{words[2]} title"
        };
        var gistWithAllExpectedWordsInSummary = CreateTestGist(feedId) with {
            Summary = $"This is a {words[0]}someextratext and {words[1]}{words[2]} summary"
        };
        var gistWithAllExpectedWords = CreateTestGist(feedId) with {
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
        var searchQuery = string.Join(' ', words);
        var gistsControllerHandler = CreateGistControllerHandler();

        var actualGists =
            await gistsControllerHandler.GetPreviousGistsAsync(10, null, [], searchQuery, [], CancellationToken.None);

        Assert.Equivalent(expectedGists, actualGists);
    }

    [Fact]
    public async Task GetPreviousGistsAsync_GistsFromDisabledFeedInDb_OnlyReturnsGistsFromEnabledFeeds()
    {
        var gistHandler = CreateGistHandler();
        var gistsFromDisabledFeed = await gistHandler.InsertTestGistsAsync(5);
        var gistsFromOtherDisabledFeed = await gistHandler.InsertTestGistsAsync(5);
        var gistsFromEnabledFeed = await gistHandler.InsertTestGistsAsync(5);
        var gistsFromOtherEnabledFeed = await gistHandler.InsertTestGistsAsync(5);
        var disabledFeedIds = new[] { gistsFromDisabledFeed.First().FeedId, gistsFromOtherDisabledFeed.First().FeedId };
        var expectedGists = new List<Gist> { gistsFromEnabledFeed.First(), gistsFromOtherEnabledFeed.First() };
        var take = gistsFromDisabledFeed.Count
                   + gistsFromOtherDisabledFeed.Count
                   + gistsFromEnabledFeed.Count
                   + gistsFromOtherEnabledFeed.Count + 5;
        var gistsControllerHandler = CreateGistControllerHandler();

        var actualGists =
            await gistsControllerHandler.GetPreviousGistsAsync(take, null, [], null, disabledFeedIds,
                CancellationToken.None);

        Assert.Equivalent(expectedGists, actualGists);
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

        Assert.Equivalent(expected, actual);
    }

    [Fact]
    public async Task GetLatestRecapAsync_NoDailyRecapsExist_ReturnsNull()
    {
        var handler = CreateGistControllerHandler();

        var actual = await handler.GetLatestRecapAsync(RecapType.Daily, CancellationToken.None);

        Assert.Null(actual);
    }

    [Fact]
    public async Task GetLatestRecapAsync_NoWeeklyRecapsExist_ReturnsNull()
    {
        var handler = CreateGistControllerHandler();

        var actual = await handler.GetLatestRecapAsync(RecapType.Weekly, CancellationToken.None);

        Assert.Null(actual);
    }

    [Fact]
    public async Task GetLatestRecapAsync_DailyRecapsExist_ReturnsLatestDailyRecap()
    {
        var dateTimeHandler = Substitute.For<IDateTimeHandler>();
        var recapHandler = CreateRecapHandler(dateTimeHandler);
        var expectedCategoryRecaps = CreateTestRecap();
        var now = DateTime.UtcNow;
        dateTimeHandler.GetUtcNow().Returns(now.AddDays(-2));
        await recapHandler.InsertDailyRecapAsync(CreateTestRecap(), CancellationToken.None);
        dateTimeHandler.GetUtcNow().Returns(now);
        await recapHandler.InsertDailyRecapAsync(expectedCategoryRecaps, CancellationToken.None);
        dateTimeHandler.GetUtcNow().Returns(now.AddDays(-1));
        await recapHandler.InsertDailyRecapAsync(CreateTestRecap(), CancellationToken.None);
        dateTimeHandler.GetUtcNow().Returns(now);
        var expected = new Recap(now, expectedCategoryRecaps);
        var gistsControllerHandler = CreateGistControllerHandler(dateTimeHandler);

        var actual = await gistsControllerHandler.GetLatestRecapAsync(RecapType.Daily, CancellationToken.None);

        Assert.NotNull(actual);
        Assert.Equivalent(expected, actual);
    }

    [Fact]
    public async Task GetLatestRecapAsync_WeeklyRecapsExist_ReturnsLatestWeeklyRecap()
    {
        var dateTimeHandler = Substitute.For<IDateTimeHandler>();
        var recapHandler = CreateRecapHandler(dateTimeHandler);
        var expectedCategoryRecaps = CreateTestRecap();
        var now = DateTime.UtcNow;
        dateTimeHandler.GetUtcNow().Returns(now.AddDays(-2));
        await recapHandler.InsertWeeklyRecapAsync(CreateTestRecap(), CancellationToken.None);
        dateTimeHandler.GetUtcNow().Returns(now);
        await recapHandler.InsertWeeklyRecapAsync(expectedCategoryRecaps, CancellationToken.None);
        dateTimeHandler.GetUtcNow().Returns(now.AddDays(-1));
        await recapHandler.InsertWeeklyRecapAsync(CreateTestRecap(), CancellationToken.None);
        dateTimeHandler.GetUtcNow().Returns(now);
        var expected = new Recap(now, expectedCategoryRecaps);
        var gistsControllerHandler = CreateGistControllerHandler(dateTimeHandler);

        var actual = await gistsControllerHandler.GetLatestRecapAsync(RecapType.Weekly, CancellationToken.None);

        Assert.NotNull(actual);
        Assert.Equivalent(expected, actual);
    }

    private MariaDbHandler CreateGistHandler(IDateTimeHandler? dateTimeHandler = null) =>
        CreateMariaDbHandler(_gistHandlerOptions, dateTimeHandler);

    private MariaDbHandler CreateRecapHandler(IDateTimeHandler? dateTimeHandler = null) =>
        CreateMariaDbHandler(_recapHandlerOptions, dateTimeHandler);

    private MariaDbHandler CreateGistControllerHandler(IDateTimeHandler? dateTimeHandler = null) =>
        CreateMariaDbHandler(_gistControllerHandlerOptions, dateTimeHandler);

    private static MariaDbHandler
        CreateMariaDbHandler(MariaDbHandlerOptions options, IDateTimeHandler? dateTimeHandler) =>
        new(Options.Create(options), dateTimeHandler ?? new DateTimeHandler(), null);
}
