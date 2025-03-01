using GistBackend.Exceptions;
using GistBackend.Handler;
using GistBackend.IntegrationTest.Utils;
using GistBackend.Types;
using Microsoft.Extensions.Options;
using MySqlConnector;

namespace GistBackend.IntegrationTest;

public class MariaDbHandlerTests(MariaDbFixture fixture) : IClassFixture<MariaDbFixture> {
    private readonly Random _random = new();

    private readonly IOptions<MariaDbHandlerOptions> _handlerOptions = Options.Create(new MariaDbHandlerOptions(
        fixture.Hostname, MariaDbFixture.GistServiceDbUsername, MariaDbFixture.GistServiceDbPassword,
        fixture.ExposedPort
    ));

    private MariaDbAsserter Asserter => new(_handlerOptions);

    private MariaDbHandler CreateHandler() => new(_handlerOptions, null);

    [Fact]
    public async Task InsertFeedInfoAsync_FeedInfoDidNotExist_FeedInfoIsInsertedInDb()
    {
        var handler = CreateHandler();
        var feedInfoToInsert = CreateTestFeedInfo();

        await handler.InsertFeedInfoAsync(feedInfoToInsert, CancellationToken.None);

        await Asserter.AssertFeedInfoIsInDbAsync(feedInfoToInsert);
    }

    [Fact]
    public async Task InsertFeedInfoAsync_FeedInfoAlreadyExists_ThrowsMySqlException()
    {
        var handler = CreateHandler();
        var feedInfoToInsert = CreateTestFeedInfo();
        await handler.InsertFeedInfoAsync(feedInfoToInsert, CancellationToken.None);

        await Assert.ThrowsAsync<MySqlException>(() =>
            handler.InsertFeedInfoAsync(feedInfoToInsert, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateFeedInfoAsync_DifferentTitle_TitleChanged()
    {
        var handler = CreateHandler();
        var feedInfoToUpdate = CreateTestFeedInfo();
        var feedInfoId = await handler.InsertFeedInfoAsync(feedInfoToUpdate, CancellationToken.None);
        var expectedFeedInfo = feedInfoToUpdate with { Title = "different title" };

        await handler.UpdateFeedInfoAsync(expectedFeedInfo, CancellationToken.None);

        await Asserter.AssertFeedInfoIsInDbAsync(expectedFeedInfo with { Id = feedInfoId });
    }

    [Fact]
    public async Task UpdateFeedInfoAsync_DifferentLanguage_LanguageChanged()
    {
        var handler = CreateHandler();
        var feedInfoToUpdate = CreateTestFeedInfo();
        var feedInfoId = await handler.InsertFeedInfoAsync(feedInfoToUpdate, CancellationToken.None);
        var expectedFeedInfo = feedInfoToUpdate with { Language = "different language" };

        await handler.UpdateFeedInfoAsync(expectedFeedInfo, CancellationToken.None);

        await Asserter.AssertFeedInfoIsInDbAsync(expectedFeedInfo with { Id = feedInfoId });
    }

    [Fact]
    public async Task UpdateFeedInfoAsync_FeedInfoDoesNotExist_ThrowsDatabaseOperationException()
    {
        var handler = CreateHandler();
        var feedInfoToUpdate = CreateTestFeedInfo();

        await Assert.ThrowsAsync<DatabaseOperationException>(() =>
            handler.UpdateFeedInfoAsync(feedInfoToUpdate, CancellationToken.None));
    }

    [Fact]
    public async Task GetFeedInfoByRssUrlAsync_FeedInfoDoesNotExist_Null()
    {
        var handler = CreateHandler();

        var actualFeedInfo = await handler.GetFeedInfoByRssUrlAsync("test rss url", CancellationToken.None);

        Assert.Null(actualFeedInfo);
    }

    [Fact]
    public async Task GetFeedInfoByRssUrlAsync_OnlyOneFeedInfoInDb_CorrectFeedInfo()
    {
        var handler = CreateHandler();
        var expectedFeedInfo = CreateTestFeedInfo();
        var feedInfoId = await handler.InsertFeedInfoAsync(expectedFeedInfo, CancellationToken.None);

        var actualFeedInfo = await handler.GetFeedInfoByRssUrlAsync(expectedFeedInfo.RssUrl, CancellationToken.None);

        Assert.Equal(expectedFeedInfo with { Id = feedInfoId }, actualFeedInfo);
    }

    [Fact]
    public async Task GetFeedInfoByRssUrlAsync_MultipleFeedInfosInDb_CorrectFeedInfo()
    {
        var handler = CreateHandler();
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
    public async Task InsertGistAsync_GistDidNotExist_GistIsInsertedInDb()
    {
        var handler = CreateHandler();
        var feedInfo = CreateTestFeedInfo();
        var feedInfoId = await handler.InsertFeedInfoAsync(feedInfo, CancellationToken.None);
        var gistToInsert = CreateTestGist(feedInfoId);

        await handler.InsertGistAsync(gistToInsert, CancellationToken.None);

        await Asserter.AssertGistIsInDbAsync(gistToInsert);
    }

    [Fact]
    public async Task InsertGistAsync_GistAlreadyExists_ThrowsMySqlException()
    {
        var handler = CreateHandler();
        var feedInfo = CreateTestFeedInfo();
        var feedInfoId = await handler.InsertFeedInfoAsync(feedInfo, CancellationToken.None);
        var gistToInsert = CreateTestGist(feedInfoId);
        await handler.InsertGistAsync(gistToInsert, CancellationToken.None);

        await Assert.ThrowsAsync<MySqlException>(() => handler.InsertGistAsync(gistToInsert, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateGistAsync_EverythingDifferentExceptReference_InformationUpdated()
    {
        var handler = CreateHandler();
        var feedInfo = CreateTestFeedInfo();
        var feedInfoId = await handler.InsertFeedInfoAsync(feedInfo, CancellationToken.None);
        var existingGist = CreateTestGist(feedInfoId);
        var gistId = await handler.InsertGistAsync(existingGist, CancellationToken.None);
        var gistToUpdate = CreateTestGist(feedInfoId) with { Reference = existingGist.Reference };

        await handler.UpdateGistAsync(gistToUpdate, CancellationToken.None);

        await Asserter.AssertGistIsInDbAsync(gistToUpdate with { Id = gistId });
    }

    [Fact]
    public async Task UpdateGistAsync_GistDoesNotExist_ThrowsDatabaseOperationException()
    {
        var handler = CreateHandler();
        var feedInfo = CreateTestFeedInfo();
        var feedInfoId = await handler.InsertFeedInfoAsync(feedInfo, CancellationToken.None);
        var gistToUpdate = CreateTestGist(feedInfoId);

        await Assert.ThrowsAsync<DatabaseOperationException>(() =>
            handler.UpdateGistAsync(gistToUpdate, CancellationToken.None));
    }

    [Fact]
    public async Task GetGistByReferenceAsync_GistDoesNotExist_Null()
    {
        var handler = CreateHandler();

        var actualGist = await handler.GetGistByReferenceAsync("test reference", CancellationToken.None);

        Assert.Null(actualGist);
    }

    [Fact]
    public async Task GetGistByReferenceAsync_OnlyOneGistInDb_CorrectGist()
    {
        var handler = CreateHandler();
        var feedInfo = CreateTestFeedInfo();
        var feedInfoId = await handler.InsertFeedInfoAsync(feedInfo, CancellationToken.None);
        var expectedGist = CreateTestGist(feedInfoId);
        var gistId = await handler.InsertGistAsync(expectedGist, CancellationToken.None);

        var actualGist = await handler.GetGistByReferenceAsync(expectedGist.Reference, CancellationToken.None);

        Assert.Equal(expectedGist with { Id = gistId }, actualGist);
    }

    [Fact]
    public async Task GetGistByReferenceAsync_MultipleGistsInDb_CorrectGist()
    {
        var handler = CreateHandler();
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

    private RssFeedInfo CreateTestFeedInfo() => new(
        _random.NextString(),
        _random.NextString(),
        _random.NextString()
    );
}
