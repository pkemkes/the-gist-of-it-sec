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
        const string query = "SELECT Title, RssUrl, Language, Type, Id FROM Feeds WHERE RssUrl = @RssUrl";
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
            SELECT Reference, FeedId, Author, IsSponsoredContent, Published, Updated, Url, Tags, Id
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

    public async Task AssertSummaryIsInDbAsync(Summary expectedSummary)
    {
        const string query = """
            SELECT GistId, Language, IsTranslated, Title, SummaryText, Id
                FROM Summaries WHERE GistId = @GistId AND Language = @Language
        """;
        var command = new CommandDefinition(query, expectedSummary);

        await using var connection = await GetOpenConnectionAsync();
        var summariesInDb = (await connection.QueryAsync<Summary>(command)).ToArray();

        Assert.Single(summariesInDb);
        var actualSummary = summariesInDb.Single();
        expectedSummary.Id = actualSummary.Id;
        Assert.Equal(expectedSummary, actualSummary);
    }

    public async Task AssertRecapIsInDbAsync(RecapAIResponse expectedRecapAIResponse, DateTime expectedCreated,
        RecapType recapType)
    {
        var query = $"SELECT Created, RecapEn, RecapDe, Id FROM Recaps{recapType.ToTypeString()} WHERE Created = @Created";
        var command = new CommandDefinition(query, new { Created = expectedCreated });

        await using var connection = await GetOpenConnectionAsync();
        var recapsInDb = (await connection.QueryAsync<SerializedRecap>(command)).ToList();

        Assert.Single(recapsInDb);
        var actualRecap = recapsInDb.Single();
        var deserializedActualRecapDe =
            JsonSerializer.Deserialize<IEnumerable<RecapSection>>(actualRecap.RecapDe, SerializerDefaults.JsonOptions);
        Assert.Equivalent(expectedRecapAIResponse.RecapSectionsGerman, deserializedActualRecapDe);
        var deserializedActualRecapEn =
            JsonSerializer.Deserialize<IEnumerable<RecapSection>>(actualRecap.RecapEn, SerializerDefaults.JsonOptions);
        Assert.Equivalent(expectedRecapAIResponse.RecapSectionsEnglish, deserializedActualRecapEn);
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
