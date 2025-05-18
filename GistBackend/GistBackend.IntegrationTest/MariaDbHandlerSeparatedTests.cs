using GistBackend.Handler;
using GistBackend.Handler.MariaDbHandler;
using GistBackend.IntegrationTest.Utils;
using GistBackend.Types;
using Microsoft.Extensions.Options;
using static GistBackend.IntegrationTest.Utils.TestData;

namespace GistBackend.IntegrationTest;

public class MariaDbHandlerSeparatedTests : IDisposable
{
    private readonly Random _random = new();
    private readonly MariaDbFixture _fixture;

    private readonly MariaDbHandlerOptions _gistHandlerOptions;

    public MariaDbHandlerSeparatedTests()
    {
        _fixture = new MariaDbFixture();
        _fixture.InitializeAsync().GetAwaiter().GetResult();

        _gistHandlerOptions = new MariaDbHandlerOptions(
            _fixture.Hostname,
            MariaDbFixture.GistServiceDbUsername,
            MariaDbFixture.GistServiceDbPassword,
            _fixture.ExposedPort
        );
    }

    public void Dispose() => _fixture.Dispose();

    private MariaDbAsserter GistAsserter => new(_gistHandlerOptions);

    [Fact]
    public async Task GetPreviousGistsAsync_NoGists_ReturnsEmptyList()
    {
        var handler = CreateGistHandler();

        var gists = await handler.GetPreviousGistsAsync(10, null, [], null, [], CancellationToken.None);

        Assert.Empty(gists);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(8)]
    public async Task GetPreviousGistsAsync_LessGistsThanTake_ReturnsAllGists(int gistCount)
    {
        var handler = CreateGistHandler();
        var expectedGists = await handler.InsertTestGistsAsync(gistCount);

        var actualGists = await handler.GetPreviousGistsAsync(gistCount+5, null, [], null, [], CancellationToken.None);

        Assert.Equivalent(expectedGists, actualGists);
    }

    [Fact]
    public async Task GetPreviousGistsAsync_MoreGistsThanTake_ReturnsAsManyGistsAsExpected()
    {
        var handler = CreateGistHandler();
        const int take = 5;
        var testGists = await handler.InsertTestGistsAsync(take+5);
        var expectedGists = testGists.Take(take).ToList();

        var actualGists = await handler.GetPreviousGistsAsync(take, null, [], null, [], CancellationToken.None);

        Assert.Equivalent(expectedGists, actualGists);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(8)]
    public async Task GetPreviousGistsAsync_LastGistGivenId_ReturnsGistsAfterLastGistId(int take)
    {
        var handler = CreateGistHandler();
        var testGists = await handler.InsertTestGistsAsync(10);
        var firstHalfOfGists = testGists.Skip(5).ToList();
        var lastGistId = testGists[4].Id;
        var expectedGists = firstHalfOfGists.Take(take).ToList();

        var actualGists = await handler.GetPreviousGistsAsync(take, lastGistId, [], null, [], CancellationToken.None);

        Assert.Equivalent(expectedGists, actualGists);
    }

    [Fact]
    public async Task GetPreviousGistsAsync_QuerySpecificTags_ReturnsGistsWithAllTags()
    {
        var tags = new[] { "tag1", "tag2", "tag3" };
        var handler = CreateGistHandler();
        var feedId = await handler.InsertFeedInfoAsync(CreateTestFeedInfo(), CancellationToken.None);
        var gistWithoutExpectedTags = CreateTestGist(feedId);
        var gistWithOnlyOneExpectedTag = CreateTestGist(feedId) with {
            Tags = string.Join(";;", tags.First())
        };
        var gistWithExpectedTags = CreateTestGist(feedId) with { Tags = string.Join(";;", tags) };
        var gistWithExpectedAndOtherTags = CreateTestGist(feedId) with {
            Tags = string.Join(";;", tags.Concat(_random.NextArrayOfStrings(3)))
        };
        await handler.InsertGistAsync(gistWithoutExpectedTags, CancellationToken.None);
        await handler.InsertGistAsync(gistWithOnlyOneExpectedTag, CancellationToken.None);
        gistWithExpectedTags.Id = await handler.InsertGistAsync(gistWithExpectedTags, CancellationToken.None);
        gistWithExpectedAndOtherTags.Id =
            await handler.InsertGistAsync(gistWithExpectedAndOtherTags, CancellationToken.None);
        var expectedGists = new List<Gist>
            { gistWithExpectedTags, gistWithExpectedAndOtherTags };

        var actualGists = await handler.GetPreviousGistsAsync(10, null, tags, null, [], CancellationToken.None);

        Assert.Equivalent(expectedGists, actualGists);
    }

    [Fact]
    public async Task GetPreviousGistsAsync_QueryWordsFromTitleAndSummary_ReturnsGistsWithAllWords()
    {
        var words = new[] { "word1", "word2", "word3" };
        var handler = CreateGistHandler();
        var feedId = await handler.InsertFeedInfoAsync(CreateTestFeedInfo(), CancellationToken.None);
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
        await handler.InsertGistAsync(gistWithoutExpectedWords, CancellationToken.None);
        await handler.InsertGistAsync(gistWithOnlyOneExpectedWordInTitle, CancellationToken.None);
        gistWithAllExpectedWordsInTitle.Id =
            await handler.InsertGistAsync(gistWithAllExpectedWordsInTitle, CancellationToken.None);
        gistWithAllExpectedWordsInSummary.Id =
            await handler.InsertGistAsync(gistWithAllExpectedWordsInSummary, CancellationToken.None);
        gistWithAllExpectedWords.Id = await handler.InsertGistAsync(gistWithAllExpectedWords, CancellationToken.None);
        var expectedGists = new List<Gist>
            { gistWithAllExpectedWords, gistWithAllExpectedWordsInTitle, gistWithAllExpectedWordsInSummary };
        var searchQuery = string.Join(' ', words);

        var actualGists = await handler.GetPreviousGistsAsync(10, null, [], searchQuery, [], CancellationToken.None);

        Assert.Equivalent(expectedGists, actualGists);
    }

    [Fact]
    public async Task GetPreviousGistsAsync_GistsFromDisabledFeedInDb_OnlyReturnsGistsFromEnabledFeeds()
    {
        var handler = CreateGistHandler();
        var gistsFromDisabledFeed = await handler.InsertTestGistsAsync(5);
        var gistsFromOtherDisabledFeed = await handler.InsertTestGistsAsync(5);
        var gistsFromEnabledFeed = await handler.InsertTestGistsAsync(5);
        var gistsFromOtherEnabledFeed = await handler.InsertTestGistsAsync(5);
        var disabledFeedIds = new[] { gistsFromDisabledFeed.First().FeedId, gistsFromOtherDisabledFeed.First().FeedId };
        var expectedGists = new List<Gist> { gistsFromEnabledFeed.First(), gistsFromOtherEnabledFeed.First() };
        var take = gistsFromDisabledFeed.Count
                   + gistsFromOtherDisabledFeed.Count
                   + gistsFromEnabledFeed.Count
                   + gistsFromOtherEnabledFeed.Count + 5;

        var actualGists =
            await handler.GetPreviousGistsAsync(take, null, [], null, disabledFeedIds, CancellationToken.None);

        Assert.Equivalent(expectedGists, actualGists);
    }

    private MariaDbHandler CreateGistHandler() =>
        new(Options.Create(_gistHandlerOptions), new DateTimeHandler(), null);
}
