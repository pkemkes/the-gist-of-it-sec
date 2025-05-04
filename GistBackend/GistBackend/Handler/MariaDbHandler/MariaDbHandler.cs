using System.Text.Json;
using Dapper;
using GistBackend.Exceptions;
using GistBackend.Types;
using GistBackend.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MySqlConnector;

namespace GistBackend.Handler.MariaDbHandler;

public interface IMariaDbHandler {
    public Task<RssFeedInfo?> GetFeedInfoByRssUrlAsync(string rssUrl, CancellationToken ct);
    public Task<int> InsertFeedInfoAsync(RssFeedInfo feedInfo, CancellationToken ct);
    public Task UpdateFeedInfoAsync(RssFeedInfo feedInfo, CancellationToken ct);
    public Task<Gist?> GetGistByReferenceAsync(string reference, CancellationToken ct);
    public Task<int> InsertGistAsync(Gist gist, CancellationToken ct);
    public Task UpdateGistAsync(Gist gist, CancellationToken ct);
    public Task<List<GoogleSearchResult>> GetSearchResultsByGistIdAsync(int gistId, CancellationToken ct);
    public Task InsertSearchResultsAsync(IEnumerable<GoogleSearchResult> searchResults, CancellationToken ct);
    public Task UpdateSearchResultsAsync(IEnumerable<GoogleSearchResult> searchResults, CancellationToken ct);
    public Task<bool> DailyRecapExistsAsync(CancellationToken ct);
    public Task<bool> WeeklyRecapExistsAsync(CancellationToken ct);
    public Task<List<Gist>> GetGistsOfLastDayAsync(CancellationToken ct);
    public Task<List<Gist>> GetGistsOfLastWeekAsync(CancellationToken ct);
    public Task InsertDailyRecapAsync(IEnumerable<CategoryRecap> recap, CancellationToken ct);
    public Task InsertWeeklyRecapAsync(IEnumerable<CategoryRecap> recap, CancellationToken ct);
}

public class MariaDbHandler(
    IOptions<MariaDbHandlerOptions> options,
    IDateTimeHandler dateTimeHandler,
    ILogger<MariaDbHandler>? logger) : IMariaDbHandler
{
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

        try
        {
            await using var connection = await GetOpenConnectionAsync(ct);
            var feedInfos = (await connection.QueryAsync<RssFeedInfo>(command)).ToArray();
            if (feedInfos.Length > 1) throw new DatabaseOperationException("Found multiple feeds in database");

            return feedInfos.FirstOrDefault();
        }
        catch (Exception e) when (e is MySqlException or DatabaseOperationException)
        {
            logger?.LogError(e, "Getting feedInfo by rssUrl failed");
            throw;
        }
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

        try
        {
            await using var connection = await GetOpenConnectionAsync(ct);
            var gists = (await connection.QueryAsync<Gist>(command)).ToArray();
            if (gists.Length > 1) throw new DatabaseOperationException("Found multiple gists in database");

            return gists.FirstOrDefault();
        }
        catch (Exception e) when (e is MySqlException or DatabaseOperationException)
        {
            logger?.LogError(e, "Getting gist by reference failed");
            throw;
        }
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

        try
        {
            await using var connection = await GetOpenConnectionAsync(ct);
            return await connection.ExecuteScalarAsync<int>(command);
        }
        catch (MySqlException e)
        {
            logger?.LogError(e, "Inserting Gist failed");
            throw;
        }
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

    public async Task<List<GoogleSearchResult>> GetSearchResultsByGistIdAsync(int gistId, CancellationToken ct)
    {
        const string query = """
            SELECT Title, Snippet, Url, DisplayUrl, ThumbnailUrl, GistId FROM SearchResults
                WHERE GistId = @GistId;
        """;
        var command = new CommandDefinition(query, new { GistId = gistId }, cancellationToken: ct);

        try
        {
            await using var connection = await GetOpenConnectionAsync(ct);
            return (await connection.QueryAsync<GoogleSearchResult>(command)).ToList();
        }
        catch (MySqlException e)
        {
            logger?.LogError(e, "Getting search results failed");
            throw;
        }
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
                (GistId, Title, Snippet, Url, DisplayUrl, ThumbnailUrl)
                VALUES (@GistId, @Title, @Snippet, @Url, @DisplayUrl, @ThumbnailUrl)
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

    public Task<bool> DailyRecapExistsAsync(CancellationToken ct) => RecapExistsAsync(RecapType.Daily, ct);

    public Task<bool> WeeklyRecapExistsAsync(CancellationToken ct) => RecapExistsAsync(RecapType.Weekly, ct);

    private async Task<bool> RecapExistsAsync(RecapType recapType, CancellationToken ct)
    {
        var query = $"SELECT COUNT(id) FROM Recaps{recapType.ToTypeString()}" +
            " WHERE Created >= @EarliestCreated AND Created <= @Now";
        var now = dateTimeHandler.GetUtcNow();
        // We always want to check whether the last recap was created in the last 24 hours
        // because we want to create new recaps every day, even if it is the weekly one.
        var command = new CommandDefinition(query, new { Now = now, EarliestCreated = now.AddDays(-1) },
            cancellationToken: ct);

        try
        {
            await using var connection = await GetOpenConnectionAsync(ct);
            var recapCount = await connection.QuerySingleAsync<int>(command);
            return recapCount switch {
                0 => false,
                1 => true,
                _ => throw new DatabaseOperationException("Found multiple recaps in database")
            };
        }
        catch (Exception e) when (e is MySqlException or DatabaseOperationException)
        {
            logger?.LogError(e, "Checked if the {RecapType} recap exists failed", recapType.ToTypeString());
            throw;
        }
    }

    public Task<List<Gist>> GetGistsOfLastDayAsync(CancellationToken ct) => GetGistsOfLastDaysAsync(1, ct);

    public Task<List<Gist>> GetGistsOfLastWeekAsync(CancellationToken ct) => GetGistsOfLastDaysAsync(7, ct);

    private async Task<List<Gist>> GetGistsOfLastDaysAsync(int days, CancellationToken ct)
    {
        const string query = """
            SELECT Reference, FeedId, Author, Title, Published, Updated, Url, Summary, Tags, SearchQuery, Id
            FROM Gists WHERE updated >= @EarliestUpdated AND updated <= @Now
        """;
        var now = dateTimeHandler.GetUtcNow();
        var earliestUpdated = now.AddDays(-days);
        var command = new CommandDefinition(query, new { Now = now, EarliestUpdated = earliestUpdated },
            cancellationToken: ct);

        try
        {
            await using var connection = await GetOpenConnectionAsync(ct);
            return (await connection.QueryAsync<Gist>(command)).ToList();
        }
        catch (MySqlException e)
        {
            logger?.LogError(e, "Getting the gists of the last {Days} days failed", days);
            throw;
        }
    }

    public Task InsertDailyRecapAsync(IEnumerable<CategoryRecap> recap, CancellationToken ct) =>
        InsertRecapAsync(RecapType.Daily, recap, ct);

    public Task InsertWeeklyRecapAsync(IEnumerable<CategoryRecap> recap, CancellationToken ct) =>
        InsertRecapAsync(RecapType.Weekly, recap, ct);

    private async Task InsertRecapAsync(RecapType recapType, IEnumerable<CategoryRecap> recap, CancellationToken ct)
    {
        var query = $"INSERT INTO Recaps{recapType.ToTypeString()} (created, recap) VALUES (@Created, @Recap)";
        var serializedRecap = new SerializedRecap(
            dateTimeHandler.GetUtcNow(),
            JsonSerializer.Serialize(recap, SerializerDefaults.JsonOptions)
        );
        var command = new CommandDefinition(query, serializedRecap, cancellationToken: ct);

        try
        {
            await using var connection = await GetOpenConnectionAsync(ct);
            await connection.ExecuteAsync(command);
        }
        catch (MySqlException e)
        {
            logger?.LogError(e, "Inserting {RecapType} recap failed", recapType.ToTypeString());
            throw;
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
