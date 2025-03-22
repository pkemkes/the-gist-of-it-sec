using Dapper;
using GistBackend.Exceptions;
using GistBackend.Types;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MySqlConnector;

namespace GistBackend.Handler;

public record MariaDbHandlerOptions(
    string Server,
    string User,
    string Password,
    uint Port = 3306
) {
    public readonly string Database = "TheGistOfItSec";
};

public interface IMariaDbHandler {
    public Task<RssFeedInfo?> GetFeedInfoByRssUrlAsync(string rssUrl, CancellationToken ct);
    public Task<int> InsertFeedInfoAsync(RssFeedInfo feedInfo, CancellationToken ct);
    public Task UpdateFeedInfoAsync(RssFeedInfo feedInfo, CancellationToken ct);
    public Task<Gist?> GetGistByReferenceAsync(string reference, CancellationToken ct);
    public Task<int> InsertGistAsync(Gist gist, CancellationToken ct);
    public Task UpdateGistAsync(Gist gist, CancellationToken ct);
    public Task<GoogleSearchResult[]> GetSearchResultsByGistIdAsync(int gistId, CancellationToken ct);
    public Task InsertSearchResultsAsync(IEnumerable<GoogleSearchResult> searchResults, CancellationToken ct);
    public Task UpdateSearchResultsAsync(IEnumerable<GoogleSearchResult> searchResults, CancellationToken ct);
}

public class MariaDbHandler(IOptions<MariaDbHandlerOptions> options, ILogger<MariaDbHandler>? logger) : IMariaDbHandler {
    private readonly string _connectionString = new MySqlConnectionStringBuilder {
        Server = options.Value.Server,
        Port = options.Value.Port,
        Database = options.Value.Database,
        UserID = options.Value.User,
        Password = options.Value.Password
    }.ConnectionString;

    public async Task<RssFeedInfo?> GetFeedInfoByRssUrlAsync(string rssUrl, CancellationToken ct)
    {
        const string query = "SELECT Title, RssUrl, Language, Id FROM Feeds WHERE RssUrl = @RssUrl";
        var command = new CommandDefinition(query, new { RssUrl = rssUrl }, cancellationToken: ct);

        await using var connection = await GetOpenConnectionAsync(ct);

        var feedInfos = (await connection.QueryAsync<RssFeedInfo>(command)).ToArray();
        if (feedInfos.Length > 1) throw new Exception("Found multiple feeds in database");

        return feedInfos.FirstOrDefault();
    }

    public async Task<int> InsertFeedInfoAsync(RssFeedInfo feedInfo, CancellationToken ct)
    {
        const string query = """
            INSERT INTO Feeds (Title, RssUrl, Language)
                VALUES (@Title, @RssUrl, @Language);
            SELECT LAST_INSERT_ID();
        """;
        var command = new CommandDefinition(query, feedInfo, cancellationToken: ct);

        await using var connection = await GetOpenConnectionAsync(ct);
        try
        {
            return await connection.ExecuteScalarAsync<int>(command);
        }
        catch (MySqlException e)
        {
            logger?.LogError(e, "Inserting FeedInfo failed");
            throw;
        }
    }

    public async Task UpdateFeedInfoAsync(RssFeedInfo feedInfo, CancellationToken ct)
    {
        const string query = "UPDATE Feeds SET Title = @Title, Language = @Language WHERE RssUrl = @RssUrl";
        var command = new CommandDefinition(query, feedInfo, cancellationToken: ct);

        await using var connection = await GetOpenConnectionAsync(ct);
        try
        {
            var rowsAffected = await connection.ExecuteAsync(command);
            if (rowsAffected != 1) throw new DatabaseOperationException("Did not successfully update feed info");
        }
        catch (Exception e) when (e is MySqlException or DatabaseOperationException)
        {
            logger?.LogError(e, "Updating FeedInfo failed");
            throw;
        }
    }

    public async Task<Gist?> GetGistByReferenceAsync(string reference, CancellationToken ct)
    {
        const string query = """
            SELECT Reference, FeedId, Author, Title, Published, Updated, Url, Summary, Tags, SearchQuery, Id
                FROM Gists WHERE Reference = @Reference
        """;
        var command = new CommandDefinition(query, new { Reference = reference }, cancellationToken: ct);

        await using var connection = await GetOpenConnectionAsync(ct);

        var gists = (await connection.QueryAsync<Gist>(command)).ToArray();
        if (gists.Length > 1) throw new Exception("Found multiple gists in database");

        return gists.FirstOrDefault();
    }

    public async Task<int> InsertGistAsync(Gist gist, CancellationToken ct)
    {
        const string query = """
            INSERT INTO Gists
                (Reference, FeedId, Author, Title, Published, Updated, Url, Summary, Tags, SearchQuery)
                VALUES (
                    @Reference, @FeedId, @Author, @Title, @Published, @Updated, @Url, @Summary, @Tags, @SearchQuery
                );
            SELECT LAST_INSERT_ID();
        """;
        var command = new CommandDefinition(query, gist, cancellationToken: ct);

        await using var connection = await GetOpenConnectionAsync(ct);
        return await connection.ExecuteScalarAsync<int>(command);
    }

    public async Task UpdateGistAsync(Gist gist, CancellationToken ct)
    {
        const string query = """
            UPDATE Gists
                SET FeedId = @FeedId, Author = @Author, Title = @Title, Published = @Published, Updated = @Updated,
                    Url = @Url, Summary = @Summary, Tags = @Tags, SearchQuery = @SearchQuery
                WHERE Reference = @Reference;
        """;
        var command = new CommandDefinition(query, gist, cancellationToken: ct);

        await using var connection = await GetOpenConnectionAsync(ct);
        try
        {
            var rowsAffected = await connection.ExecuteAsync(command);
            if (rowsAffected != 1) throw new DatabaseOperationException("Did not successfully update gist");
        }
        catch (Exception e) when (e is MySqlException or DatabaseOperationException)
        {
            logger?.LogError(e, "Updating gist failed");
            throw;
        }
    }

    public async Task<GoogleSearchResult[]> GetSearchResultsByGistIdAsync(int gistId, CancellationToken ct)
    {
        const string query = """
            SELECT Title, Snippet, Url, DisplayUrl, ThumbnailUrl, ImageUrl, GistId FROM SearchResults
                WHERE GistId = @GistId;
        """;
        var command = new CommandDefinition(query, new { GistId = gistId }, cancellationToken: ct);

        await using var connection = await GetOpenConnectionAsync(ct);

        return (await connection.QueryAsync<GoogleSearchResult>(command)).ToArray();
    }

    public async Task InsertSearchResultsAsync(IEnumerable<GoogleSearchResult> searchResults, CancellationToken ct)
    {
        await using var connection = await GetOpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        foreach (var searchResult in searchResults)
        {
            await InsertSearchResultAsync(searchResult, connection, transaction, ct);
        }
        await transaction.CommitAsync(ct);
    }

    public async Task UpdateSearchResultsAsync(IEnumerable<GoogleSearchResult> searchResults, CancellationToken ct)
    {
        await using var connection = await GetOpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        var searchResultsArray = searchResults.ToArray();
        await DeleteSearchResultsForGistIdAsync(searchResultsArray.First().GistId, connection, transaction, ct);
        foreach (var searchResult in searchResultsArray)
        {
            await InsertSearchResultAsync(searchResult, connection, transaction, ct);
        }
        await transaction.CommitAsync(ct);
    }

    private static async Task InsertSearchResultAsync(GoogleSearchResult searchResult, MySqlConnection connection,
        MySqlTransaction transaction, CancellationToken ct)
    {
        const string query = """
            INSERT INTO SearchResults
                (GistId, Title, Snippet, Url, DisplayUrl, ThumbnailUrl, ImageUrl)
                VALUES (@GistId, @Title, @Snippet, @Url, @DisplayUrl, @ThumbnailUrl, @ImageUrl)
        """;
        var command = new CommandDefinition(query, searchResult, transaction, cancellationToken: ct);
        await connection.ExecuteAsync(command);
    }

    private static async Task DeleteSearchResultsForGistIdAsync(int gistId, MySqlConnection connection,
        MySqlTransaction transaction, CancellationToken ct)
    {
        const string query = "DELETE FROM SearchResults WHERE GistId = @GistId";
        var command = new CommandDefinition(query, new { GistId = gistId }, transaction, cancellationToken: ct);
        var rowsAffected = await connection.ExecuteAsync(command);
        if (rowsAffected == 0)
        {
            throw new DatabaseOperationException("Did not delete any search results");
        }
    }

    private async Task<MySqlConnection> GetOpenConnectionAsync(CancellationToken ct)
    {
        MySqlConnection? connection = null;
        try
        {
            connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(ct);
            return connection;
        }
        catch
        {
            if (connection is not null) await connection.DisposeAsync();
            throw;
        }
    }
}
