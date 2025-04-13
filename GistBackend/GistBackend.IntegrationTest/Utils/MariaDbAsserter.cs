using Dapper;
using GistBackend.Handler;
using GistBackend.Types;
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
        expectedFeedInfo.Id ??= actualFeedInfo.Id;
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
        expectedGist.Id ??= actualGist.Id;
        Assert.Equal(expectedGist, actualGist);
    }

    public async Task AssertSearchResultsForGistIdInDbAsync(int gistId,
        IEnumerable<GoogleSearchResult> expectedSearchResults)
    {
        const string query = """
            SELECT GistId, Title, Snippet, Url, DisplayUrl, ThumbnailUrl, Id
                FROM SearchResults WHERE GistId = @GistId
        """;
        var command = new CommandDefinition(query, new { GistId = gistId });

        await using var connection = await GetOpenConnectionAsync();
        var searchResultsInDb = await connection.QueryAsync<GoogleSearchResult>(command);

        // We don't want to check whether the IDs are the same for the expected and actual searchResults
        foreach (var searchResult in expectedSearchResults.Concat(searchResultsInDb)) searchResult.Id = null;

        Assert.Equivalent(expectedSearchResults, searchResultsInDb);
    }

    private async Task<MySqlConnection> GetOpenConnectionAsync()
    {
        var connection = GetConnection();
        await connection.OpenAsync();
        return connection;
    }
}
