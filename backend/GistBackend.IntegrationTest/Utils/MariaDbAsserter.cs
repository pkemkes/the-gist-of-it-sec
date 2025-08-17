using System.Text.Json;
using Dapper;
using GistBackend.Handlers.MariaDbHandler;
using GistBackend.Types;
using GistBackend.Utils;
using MySqlConnector;

namespace GistBackend.IntegrationTest.Utils;

public class MariaDbAsserter(MariaDbHandlerOptions options) {
    private readonly string _connectionString = new MySqlConnectionStringBuilder {
        Server = options.Server,
        Port = options.Port,
        Database = options.Database,
        UserID = options.User,
        Password = options.Password
    }.ConnectionString;

    private MySqlConnection GetConnection() => new(_connectionString);

    public async Task AssertFeedInfoIsInDbAsync(RssFeedInfo expectedFeedInfo)
    {
        const string query = "SELECT Title, RssUrl, Language, Id FROM Feeds WHERE RssUrl = @RssUrl";
        var command = new CommandDefinition(query, expectedFeedInfo);

        await using var connection = await GetOpenConnectionAsync();
        var feedInfosInDb = (await connection.QueryAsync<RssFeedInfo>(command)).ToArray();

        Assert.Single(feedInfosInDb);
        var actualFeedInfo = feedInfosInDb.Single();
        expectedFeedInfo.Id = actualFeedInfo.Id;
        Assert.Equal(expectedFeedInfo, actualFeedInfo);
    }

    public async Task AssertGistIsInDbAsync(Gist expectedGist)
    {
        const string query = """
            SELECT Reference, FeedId, Author, Title, Published, Updated, Url, Summary, Tags, SearchQuery, Id
                FROM Gists WHERE Reference = @Reference
        """;
        var command = new CommandDefinition(query, expectedGist);

        await using var connection = await GetOpenConnectionAsync();
        var gistsInDb = (await connection.QueryAsync<Gist>(command)).ToArray();

        Assert.Single(gistsInDb);
        var actualGist = gistsInDb.Single();
        expectedGist.Id = actualGist.Id;
        Assert.Equal(expectedGist, actualGist);
    }

    public async Task AssertSearchResultsForGistIdInDbAsync(int gistId,
        IList<GoogleSearchResult> expectedSearchResults)
    {
        const string query = """
            SELECT GistId, Title, Snippet, Url, DisplayUrl, ThumbnailUrl, Id
                FROM SearchResults WHERE GistId = @GistId
        """;
        var command = new CommandDefinition(query, new { GistId = gistId });

        await using var connection = await GetOpenConnectionAsync();
        var searchResultsInDb = (await connection.QueryAsync<GoogleSearchResult>(command)).ToList();

        // We don't want to check whether the IDs are the same for the expected and actual searchResults
        foreach (var searchResult in expectedSearchResults.Concat(searchResultsInDb)) searchResult.Id = null;

        Assert.Equal(expectedSearchResults, searchResultsInDb);
    }

    public async Task AssertRecapIsInDbAsync(Recap expectedRecap, DateTime expectedCreated,
        RecapType recapType)
    {
        var query = $"SELECT Created, Recap, Id FROM Recaps{recapType.ToTypeString()} WHERE Created = @Created";
        var command = new CommandDefinition(query, new { Created = expectedCreated });

        await using var connection = await GetOpenConnectionAsync();
        var recapsInDb = (await connection.QueryAsync<SerializedRecap>(command)).ToList();

        Assert.Single(recapsInDb);
        var actualRecap = recapsInDb.Single();
        var deserializedActualRecap =
            JsonSerializer.Deserialize<Recap>(actualRecap.Recap, SerializerDefaults.JsonOptions);
        Assert.Equivalent(expectedRecap, deserializedActualRecap);
    }

    public Task AssertGistIsEnabledAsync(int gistId) => AssertGistDisabledStateIsAsExpectedAsync(gistId, false);

    public Task AssertGistIsDisabledAsync(int gistId) => AssertGistDisabledStateIsAsExpectedAsync(gistId, true);

    private async Task AssertGistDisabledStateIsAsExpectedAsync(int gistId, bool expected)
    {
        const string query = "SELECT Disabled FROM Gists WHERE Id = @Id";
        var command = new CommandDefinition(query, new { Id = gistId });

        await using var connection = await GetOpenConnectionAsync();
        var actual = await connection.QuerySingleAsync<bool>(command);

        Assert.Equal(expected, actual);
    }

    public async Task AssertChatIsInDbAsync(long expectedChatId, int? expectedGistIdLastSent = null)
        => Assert.Equal(1, await GetCountOfRegisteredChatsAsync(expectedChatId, expectedGistIdLastSent));

    public async Task AssertChatIsNotInDbAsync(long chatId)
        => Assert.Equal(0, await GetCountOfRegisteredChatsAsync(chatId));

    private async Task<int> GetCountOfRegisteredChatsAsync(long chatId, int? gistIdLastSent = null)
    {
        var query = "SELECT COUNT(*) FROM Chats WHERE Id = @ChatId";
        if (gistIdLastSent.HasValue) query += " AND GistIdLastSent = @GistIdLastSent";
        var command = new CommandDefinition(query, new { ChatId = chatId, GistIdLastSent = gistIdLastSent });

        await using var connection = await GetOpenConnectionAsync();
        return await connection.ExecuteScalarAsync<int>(command);
    }

    private async Task<MySqlConnection> GetOpenConnectionAsync()
    {
        var connection = GetConnection();
        await connection.OpenAsync();
        return connection;
    }
}
