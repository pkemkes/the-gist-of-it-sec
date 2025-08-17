using System.Text.Json;
using Dapper;
using GistBackend.Exceptions;
using GistBackend.Types;
using GistBackend.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MySqlConnector;
using static GistBackend.Utils.LogEvents;

namespace GistBackend.Handlers.MariaDbHandler;

public interface IMariaDbHandler {
    Task<RssFeedInfo?> GetFeedInfoByRssUrlAsync(Uri rssUrl, CancellationToken ct);
    Task<int> InsertFeedInfoAsync(RssFeedInfo feedInfo, CancellationToken ct);
    Task UpdateFeedInfoAsync(RssFeedInfo feedInfo, CancellationToken ct);
    Task<Gist?> GetGistByReferenceAsync(string reference, CancellationToken ct);
    Task<GistWithFeed?> GetGistWithFeedByReference(string reference, CancellationToken ct);
    Task<int> InsertGistAsync(Gist gist, CancellationToken ct);
    Task UpdateGistAsync(Gist gist, CancellationToken ct);
    Task<List<GoogleSearchResult>> GetSearchResultsByGistIdAsync(int gistId, CancellationToken ct);
    Task InsertSearchResultsAsync(IEnumerable<GoogleSearchResult> searchResults, CancellationToken ct);
    Task UpdateSearchResultsAsync(IEnumerable<GoogleSearchResult> searchResults, CancellationToken ct);
    Task<bool> DailyRecapExistsAsync(CancellationToken ct);
    Task<bool> WeeklyRecapExistsAsync(CancellationToken ct);
    Task<List<Gist>> GetGistsOfLastDayAsync(CancellationToken ct);
    Task<List<Gist>> GetGistsOfLastWeekAsync(CancellationToken ct);
    Task<int> InsertDailyRecapAsync(Recap recap, CancellationToken ct);
    Task<int> InsertWeeklyRecapAsync(Recap recap, CancellationToken ct);
    Task<List<Gist>> GetAllGistsAsync(CancellationToken ct);
    Task<bool> EnsureCorrectDisabledStateForGistAsync(int gistId, bool disabled, CancellationToken ct);
    Task<List<GistWithFeed>> GetPreviousGistsWithFeedAsync(int take, int? lastGistId, IEnumerable<string> tags,
        string? searchQuery, IEnumerable<int> disabledFeeds, CancellationToken ct);
    Task<GistWithFeed?> GetGistWithFeedByIdAsync(int id, CancellationToken ct);
    Task<List<RssFeedInfo>> GetAllFeedInfosAsync(CancellationToken ct);
    Task<SerializedRecap?> GetLatestRecapAsync(RecapType recapType, CancellationToken ct);
    Task<bool> IsChatRegisteredAsync(long chatId, CancellationToken ct);
    Task RegisterChatAsync(long chatId, CancellationToken ct);
    Task DeregisterChatAsync(long chatId, CancellationToken ct);
    Task<List<Chat>> GetAllChatsAsync(CancellationToken ct);
    Task<List<GistWithFeed>> GetNextFiveGistsWithFeedAsync(int lastGistId, CancellationToken ct);
    Task SetGistIdLastSentForChatAsync(long chatId, int gistId, CancellationToken ct);
}

public class MariaDbHandler : IMariaDbHandler
{
    private readonly string _connectionString;
    private readonly IDateTimeHandler _dateTimeHandler;
    private readonly ILogger<MariaDbHandler>? _logger;

    public MariaDbHandler(IOptions<MariaDbHandlerOptions> options,
        IDateTimeHandler dateTimeHandler,
        ILogger<MariaDbHandler>? logger)
    {
        _dateTimeHandler = dateTimeHandler;
        _logger = logger;
        _connectionString = options.Value.GetConnectionString();
        SqlMapper.AddTypeHandler(new UriTypeHandler());
    }

    public async Task<RssFeedInfo?> GetFeedInfoByRssUrlAsync(Uri rssUrl, CancellationToken ct)
    {
        const string query = "SELECT Title, RssUrl, Language, Id FROM Feeds WHERE RssUrl = @RssUrl";
        var command = new CommandDefinition(query, new { RssUrl = rssUrl }, cancellationToken: ct);

        try
        {
            await using var connection = await GetOpenConnectionAsync(ct);
            return await connection.QueryFirstOrDefaultAsync<RssFeedInfo>(command).WithDeadlockRetry(_logger);
        }
        catch (MySqlException e)
        {
            _logger?.LogError(GettingFeedInfoByUrlFailed, e, "Getting feedInfo by rssUrl failed");
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
            return await connection.ExecuteScalarAsync<int>(command).WithDeadlockRetry(_logger);
        }
        catch (MySqlException e)
        {
            _logger?.LogError(InsertingFeedInfoFailed, e, "Inserting FeedInfo failed");
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
            var rowsAffected = await connection.ExecuteAsync(command).WithDeadlockRetry(_logger);
            if (rowsAffected != 1) throw new DatabaseOperationException("Did not successfully update feed info");
        }
        catch (Exception e) when (e is MySqlException or DatabaseOperationException)
        {
            _logger?.LogError(UpdatingFeedInfoFailed, e, "Updating FeedInfo failed");
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
            return await connection.QueryFirstOrDefaultAsync<Gist>(command).WithDeadlockRetry(_logger);
        }
        catch (MySqlException e)
        {
            _logger?.LogError(GettingGistByReferenceFailed, e, "Getting gist by reference failed");
            throw;
        }
    }

    public async Task<GistWithFeed?> GetGistWithFeedByReference(string reference, CancellationToken ct)
    {
        const string query = """
            SELECT
                g.Id as Id,
                g.Reference as Reference,
                f.Title as FeedTitle,
                f.RssUrl as FeedUrl,
                g.Title as Title,
                g.Author as Author,
                g.Url as Url,
                DATE_FORMAT(g.Published, '%Y-%m-%dT%H:%i:%s.%fZ') as Published,
                DATE_FORMAT(g.Updated, '%Y-%m-%dT%H:%i:%s.%fZ') as Updated,
                g.Summary as Summary,
                g.Tags as Tags,
                g.SearchQuery as SearchQuery
            FROM Gists g
            INNER JOIN Feeds f ON g.FeedId = f.Id
            WHERE g.Reference = @Reference
        """;
        var command = new CommandDefinition(query, new { Reference = reference }, cancellationToken: ct);

        try
        {
            await using var connection = await GetOpenConnectionAsync(ct);
            return await connection.QueryFirstOrDefaultAsync<GistWithFeed>(command).WithDeadlockRetry(_logger);
        }
        catch (MySqlException e)
        {
            _logger?.LogError(GettingGistByReferenceFailed, e, "Getting gist by reference failed");
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
            return await connection.ExecuteScalarAsync<int>(command).WithDeadlockRetry(_logger);
        }
        catch (MySqlException e)
        {
            _logger?.LogError(InsertingGistFailed, e, "Inserting Gist failed");
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
            var rowsAffected = await connection.ExecuteAsync(command).WithDeadlockRetry(_logger);
            if (rowsAffected != 1) throw new DatabaseOperationException("Did not successfully update gist");
        }
        catch (Exception e) when (e is MySqlException or DatabaseOperationException)
        {
            _logger?.LogError(UpdatingGistFailed, e, "Updating gist failed");
            throw;
        }
    }

    public async Task<List<GoogleSearchResult>> GetSearchResultsByGistIdAsync(int gistId, CancellationToken ct)
    {
        const string query = """
            SELECT GistId, Title, Snippet, Url, DisplayUrl, ThumbnailUrl, Id FROM SearchResults
                WHERE GistId = @GistId;
        """;
        var command = new CommandDefinition(query, new { GistId = gistId }, cancellationToken: ct);

        try
        {
            await using var connection = await GetOpenConnectionAsync(ct);
            return (await connection.QueryAsync<GoogleSearchResult>(command).WithDeadlockRetry(_logger)).ToList();
        }
        catch (MySqlException e)
        {
            _logger?.LogError(GettingSearchResultsFailed, e, "Getting search results failed");
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
        await transaction.CommitAsync(ct).WithDeadlockRetry(_logger);
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
        await transaction.CommitAsync(ct).WithDeadlockRetry(_logger);
    }

    private async Task InsertSearchResultAsync(GoogleSearchResult searchResult, MySqlConnection connection,
        MySqlTransaction transaction, CancellationToken ct)
    {
        const string query = """
            INSERT INTO SearchResults
                (GistId, Title, Snippet, Url, DisplayUrl, ThumbnailUrl)
                VALUES (@GistId, @Title, @Snippet, @Url, @DisplayUrl, @ThumbnailUrl)
        """;
        var command = new CommandDefinition(query, searchResult, transaction, cancellationToken: ct);
        await connection.ExecuteAsync(command).WithDeadlockRetry(_logger);
    }

    private async Task DeleteSearchResultsForGistIdAsync(int gistId, MySqlConnection connection,
        MySqlTransaction transaction, CancellationToken ct)
    {
        const string query = "DELETE FROM SearchResults WHERE GistId = @GistId";
        var command = new CommandDefinition(query, new { GistId = gistId }, transaction, cancellationToken: ct);
        var rowsAffected = await connection.ExecuteAsync(command).WithDeadlockRetry(_logger);
        if (rowsAffected == 0)
        {
            _logger?.LogError(DeletingSearchResultsFailed,
                "Did not delete any search results for gist with ID {GistId} failed", gistId);
            throw new DatabaseOperationException("Did not delete any search results");
        }
    }

    public Task<bool> DailyRecapExistsAsync(CancellationToken ct) => RecapExistsAsync(RecapType.Daily, ct);

    public Task<bool> WeeklyRecapExistsAsync(CancellationToken ct) => RecapExistsAsync(RecapType.Weekly, ct);

    private async Task<bool> RecapExistsAsync(RecapType recapType, CancellationToken ct)
    {
        var query = $"SELECT COUNT(id) FROM Recaps{recapType.ToTypeString()}" +
            " WHERE Created >= @EarliestCreated AND Created <= @Now";
        var now = _dateTimeHandler.GetUtcNow();
        // We always want to check whether the last recap was created in the last 24 hours
        // because we want to create new recaps every day, even if it is the weekly one.
        var command = new CommandDefinition(query, new { Now = now, EarliestCreated = now.AddDays(-1) },
            cancellationToken: ct);

        try
        {
            await using var connection = await GetOpenConnectionAsync(ct);
            var recapCount = await connection.QuerySingleAsync<int>(command).WithDeadlockRetry(_logger);
            return recapCount switch {
                0 => false,
                1 => true,
                _ => throw new DatabaseOperationException("Found multiple recaps in database")
            };
        }
        catch (Exception e) when (e is MySqlException or DatabaseOperationException)
        {
            _logger?.LogError(CheckIfRecapExistsFailed, e, "Check if the {RecapType} recap exists failed",
                recapType.ToTypeString());
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
        var now = _dateTimeHandler.GetUtcNow();
        var earliestUpdated = now.AddDays(-days);
        var command = new CommandDefinition(query, new { Now = now, EarliestUpdated = earliestUpdated },
            cancellationToken: ct);

        try
        {
            await using var connection = await GetOpenConnectionAsync(ct);
            return (await connection.QueryAsync<Gist>(command).WithDeadlockRetry(_logger)).ToList();
        }
        catch (MySqlException e)
        {
            _logger?.LogError(GettingGistsForRecapFailed, e, "Getting the gists of the last {Days} days failed", days);
            throw;
        }
    }

    public Task<int> InsertDailyRecapAsync(Recap recap, CancellationToken ct) =>
        InsertRecapAsync(RecapType.Daily, recap, ct);

    public Task<int> InsertWeeklyRecapAsync(Recap recap, CancellationToken ct) =>
        InsertRecapAsync(RecapType.Weekly, recap, ct);

    private async Task<int> InsertRecapAsync(RecapType recapType, Recap recap, CancellationToken ct)
    {
        var query = $"""
            INSERT INTO Recaps{recapType.ToTypeString()} (created, recap) VALUES (@Created, @Recap);
            SELECT LAST_INSERT_ID();
        """;
        var serializedRecap = new SerializedRecap(
            _dateTimeHandler.GetUtcNow(),
            JsonSerializer.Serialize(recap, SerializerDefaults.JsonOptions)
        );
        var command = new CommandDefinition(query, serializedRecap, cancellationToken: ct);

        try
        {
            await using var connection = await GetOpenConnectionAsync(ct);
            return await connection.ExecuteScalarAsync<int>(command).WithDeadlockRetry(_logger);
        }
        catch (MySqlException e)
        {
            _logger?.LogError(e, "Inserting {RecapType} recap failed", recapType.ToTypeString());
            throw;
        }
    }

    public async Task<List<Gist>> GetAllGistsAsync(CancellationToken ct)
    {
        const string query = """
            SELECT Reference, FeedId, Author, Title, Published, Updated, Url, Summary, Tags, SearchQuery, Id
            FROM Gists
        """;
        var command = new CommandDefinition(query, cancellationToken: ct);

        try
        {
            await using var connection = await GetOpenConnectionAsync(ct);
            return (await connection.QueryAsync<Gist>(command).WithDeadlockRetry(_logger)).ToList();
        }
        catch (MySqlException e)
        {
            _logger?.LogError(GettingAllGistsFailed, e, "Getting all gists failed");
            throw;
        }
    }

    public async Task<bool> EnsureCorrectDisabledStateForGistAsync(int gistId, bool disabled, CancellationToken ct)
    {
        if (await GetDisabledStateForGistAsync(gistId, ct) == disabled) return true;
        const string query = "UPDATE Gists SET Disabled = @Disabled WHERE Id = @GistId";
        var command = new CommandDefinition(query, new { Disabled = disabled, GistId = gistId }, cancellationToken: ct);

        try
        {
            await using var connection = await GetOpenConnectionAsync(ct);
            var rowsAffected = await connection.ExecuteAsync(command).WithDeadlockRetry(_logger);
            if (rowsAffected != 1) throw new DatabaseOperationException("Did not successfully set gist disabled state");
        }
        catch (Exception e) when (e is MySqlException or DatabaseOperationException)
        {
            _logger?.LogError(EnsuringCorrectDisabledFailed, e, "Ensuring correct disabled state for gist failed");
            throw;
        }

        _logger?.LogInformation(ChangedDisabledStateOfGistInDb,
            "Changed disabled state of gist with ID {GistId} to {Disabled}", gistId, disabled);
        return false;
    }

    private async Task<bool> GetDisabledStateForGistAsync(int gistId, CancellationToken ct)
    {
        const string query = "SELECT Disabled FROM Gists WHERE Id = @GistId";
        var command = new CommandDefinition(query, new { GistId = gistId }, cancellationToken: ct);

        try
        {
            await using var connection = await GetOpenConnectionAsync(ct);
            return await connection.QuerySingleAsync<bool>(command).WithDeadlockRetry(_logger);
        }
        catch (MySqlException e)
        {
            _logger?.LogError(GettingDisabledStateFailed, e, "Getting disabled state for gist failed");
            throw;
        }
    }

    public async Task<List<GistWithFeed>> GetPreviousGistsWithFeedAsync(int take, int? lastGistId, IEnumerable<string> tags,
        string? searchQuery, IEnumerable<int> disabledFeeds, CancellationToken ct)
    {
        var parameters = new DynamicParameters();
        var constraints = new List<string> { "Disabled IS FALSE" };

        AddLastGistIdConstraint(constraints, parameters, lastGistId);
        AddSearchQueryConstraint(parameters, constraints, searchQuery);
        AddTagsConstraint(parameters, constraints, tags);
        AddDisabledFeedsConstraint(parameters, constraints, disabledFeeds);
        parameters.Add("Take", take);

        var constraintsTemplate = string.Join(" AND ", constraints);

        var query = $"""
            SELECT
                g.Id as Id,
                g.Reference as Reference,
                f.Title as FeedTitle,
                f.RssUrl as FeedUrl,
                g.Title as Title,
                g.Author as Author,
                g.Url as Url,
                DATE_FORMAT(g.Published, '%Y-%m-%dT%H:%i:%s.%fZ') as Published,
                DATE_FORMAT(g.Updated, '%Y-%m-%dT%H:%i:%s.%fZ') as Updated,
                g.Summary as Summary,
                g.Tags as Tags,
                g.SearchQuery as SearchQuery
            FROM Gists g
            INNER JOIN Feeds f ON g.FeedId = f.Id
            WHERE {constraintsTemplate}
            ORDER BY g.id DESC LIMIT @Take
        """;

        var command = new CommandDefinition(query, parameters, cancellationToken: ct);
        try
        {
            await using var connection = await GetOpenConnectionAsync(ct);
            return (await connection.QueryAsync<GistWithFeed>(command).WithDeadlockRetry(_logger)).ToList();
        }
        catch (MySqlException e)
        {
            _logger?.LogError(GettingPreviousGistsWithFeedFailed, e, "Getting previous gists with feed failed");
            throw;
        }
    }

    private static void AddLastGistIdConstraint(List<string> constraints, DynamicParameters parameters, int? lastGistId)
    {
        constraints.Add("g.Id < @LastGistId");
        parameters.Add("LastGistId", lastGistId ?? int.MaxValue);
    }

    private static void AddSearchQueryConstraint(DynamicParameters parameters, List<string> constraints, string? searchQuery)
    {
        var parsedSearchQuery = ParseSearchQuery(searchQuery);
        for (var i = 0; i < parsedSearchQuery.Count; i++)
        {
            parameters.Add($"SearchQuery{i}", parsedSearchQuery[i]);
            constraints.Add($"(LOWER(g.Title) LIKE @SearchQuery{i} OR LOWER(g.Summary) LIKE @SearchQuery{i})");
        }
    }

    private static List<string> ParseSearchQuery(string? searchQuery) => string.IsNullOrWhiteSpace(searchQuery)
        ? []
        : searchQuery
            .Split(' ')
            .Where(word => !string.IsNullOrWhiteSpace(word))
            .Select(word => word.Trim().ToLowerInvariant())
            .Select(word => $"%{word}%")
            .ToList();

    private static void AddTagsConstraint(DynamicParameters parameters, List<string> constraints, IEnumerable<string> tags)
    {
        var parsedTags = ParseTags(tags);
        for (var i = 0; i < parsedTags.Count; i++)
        {
            parameters.Add($"Tags{i}", parsedTags[i]);
            constraints.Add($"g.Tags REGEXP @Tags{i}");
        }
    }

    private static List<string> ParseTags(IEnumerable<string> tags) => tags
        .Where(tag => !string.IsNullOrWhiteSpace(tag))
        .Select(tag => $@"\b{tag}\b")
        .ToList();

    private static void AddDisabledFeedsConstraint(DynamicParameters parameters, List<string> constraints,
        IEnumerable<int> disabledFeeds)
    {
        parameters.Add("DisabledFeeds", disabledFeeds);
        constraints.Add("g.FeedId NOT IN @DisabledFeeds");
    }

    public async Task<GistWithFeed?> GetGistWithFeedByIdAsync(int id, CancellationToken ct)
    {
        const string query = """
            SELECT
                g.Id as Id,
                g.Reference as Reference,
                f.Title as FeedTitle,
                f.RssUrl as FeedUrl,
                g.Title as Title,
                g.Author as Author,
                g.Url as Url,
                DATE_FORMAT(g.Published, '%Y-%m-%dT%H:%i:%s.%fZ') as Published,
                DATE_FORMAT(g.Updated, '%Y-%m-%dT%H:%i:%s.%fZ') as Updated,
                g.Summary as Summary,
                g.Tags as Tags,
                g.SearchQuery as SearchQuery
            FROM Gists g
            INNER JOIN Feeds f ON g.FeedId = f.Id
            WHERE g.Id = @Id
        """;
        var command = new CommandDefinition(query, new { Id = id }, cancellationToken: ct);

        try
        {
            await using var connection = await GetOpenConnectionAsync(ct);
            return await connection.QuerySingleOrDefaultAsync<GistWithFeed>(command).WithDeadlockRetry(_logger);
        }
        catch (MySqlException e)
        {
            _logger?.LogError(GettingGistByReferenceFailed, e, "Getting gist by ID failed");
            throw;
        }
    }

    public async Task<List<RssFeedInfo>> GetAllFeedInfosAsync(CancellationToken ct)
    {
        const string query = "SELECT Title, RssUrl, Language, Id FROM Feeds";
        var command = new CommandDefinition(query, cancellationToken: ct);

        try
        {
            await using var connection = await GetOpenConnectionAsync(ct);
            return (await connection.QueryAsync<RssFeedInfo>(command).WithDeadlockRetry(_logger)).ToList();
        }
        catch (MySqlException e)
        {
            _logger?.LogError(GettingAllFeedInfosFailed, e, "Getting all feed infos failed");
            throw;
        }
    }

    public async Task<SerializedRecap?> GetLatestRecapAsync(RecapType recapType, CancellationToken ct)
    {
        var query = $"SELECT Created, Recap, Id FROM Recaps{recapType.ToTypeString()} ORDER BY Created DESC LIMIT 1";
        var command = new CommandDefinition(query, cancellationToken: ct);

        try
        {
            await using var connection = await GetOpenConnectionAsync(ct);
            var serializedRecap = await connection.QuerySingleOrDefaultAsync<SerializedRecap>(command)
                .WithDeadlockRetry(_logger);
            if (serializedRecap is not null) return serializedRecap;
            _logger?.LogInformation(NoRecapFound, "No {RecapType} recap found in database",
                recapType.ToTypeString());
            return null;
        }
        catch (MySqlException e)
        {
            _logger?.LogError(GettingLatestRecapFailed, e, "Getting latest {RecapType} recap failed",
                recapType.ToTypeString());
            throw;
        }
    }

    public async Task<bool> IsChatRegisteredAsync(long chatId, CancellationToken ct)
    {
        const string query = "SELECT COUNT(Id) FROM Chats WHERE Id = @ChatId";
        var command = new CommandDefinition(query, new { ChatId = chatId }, cancellationToken: ct);

        try
        {
            await using var connection = await GetOpenConnectionAsync(ct);
            var count = await connection.ExecuteScalarAsync<int>(command).WithDeadlockRetry(_logger);
            if (count > 1)
            {
                throw new DatabaseOperationException($"Found multiple chats with the same ID {chatId} in database");
            }
            return count > 0;
        }
        catch (MySqlException e)
        {
            _logger?.LogError(ChatRegisterCheckFailed, e, "Checking if chat is registered failed");
            throw;
        }
    }

    public async Task RegisterChatAsync(long chatId, CancellationToken ct)
    {
        const string query = "INSERT INTO Chats (Id, GistIdLastSent) VALUES (@ChatId, @GistIdLastSent)";
        var mostRecentGistWithFeed = await GetMostRecentGistWithFeedAsync(ct);
        // Default to 0 if no gists are found, so that the first gist will be sent
        // otherwise set it to 5 less than the most recent gist ID to send the last 5 gists
        var gistIdLastSent = mostRecentGistWithFeed?.Id - 5 ?? 0;
        var command = new CommandDefinition(query, new { ChatId = chatId, GistIdLastSent = gistIdLastSent },
            cancellationToken: ct);

        try
        {
            await using var connection = await GetOpenConnectionAsync(ct);
            await connection.ExecuteAsync(command).WithDeadlockRetry(_logger);
            _logger?.LogInformation(ChatRegistered,
                "Chat with ID {ChatId} registered with GistIdLastSend {GistIdLastSend}", chatId, gistIdLastSent);
        }
        catch (MySqlException e)
        {
            _logger?.LogError(RegisteringChatFailed, e,
                "Registering chat with ID {ChatId} and GistIdLastSend {GistIdLastSend} failed", chatId, gistIdLastSent);
            throw;
        }
    }

    private async Task<GistWithFeed?> GetMostRecentGistWithFeedAsync(CancellationToken ct)
    {
        var gistsWithFeed = await GetPreviousGistsWithFeedAsync(1, null, [], null, [], ct);
        if (gistsWithFeed.Count != 0) return gistsWithFeed.Single();
        _logger?.LogInformation(NoRecentGistFound, "No recent gist found in database");
        return null;
    }

    public async Task DeregisterChatAsync(long chatId, CancellationToken ct)
    {
        const string query = "DELETE FROM Chats WHERE Id = @ChatId";
        var command = new CommandDefinition(query, new { ChatId = chatId }, cancellationToken: ct);

        try
        {
            await using var connection = await GetOpenConnectionAsync(ct);
            var rowsAffected = await connection.ExecuteAsync(command).WithDeadlockRetry(_logger);
            switch (rowsAffected)
            {
                case 0:
                    throw new DatabaseOperationException($"No chat with ID {chatId} found to deregister");
                case > 1:
                    throw new DatabaseOperationException($"Deregistered multiple chats with the same ID {chatId}");
                default:
                    _logger?.LogInformation(ChatDeregistered, "Chat with ID {ChatId} deregistered", chatId);
                    break;
            }
        }
        catch (MySqlException e)
        {
            _logger?.LogError(DeregisteringChatFailed, e, "Deregistering chat with ID {ChatId} failed", chatId);
            throw;
        }
    }

    public async Task<List<Chat>> GetAllChatsAsync(CancellationToken ct)
    {
        const string query = "SELECT Id, GistIdLastSent FROM Chats";
        var command = new CommandDefinition(query, cancellationToken: ct);

        try
        {
            await using var connection = await GetOpenConnectionAsync(ct);
            return (await connection.QueryAsync<Chat>(command).WithDeadlockRetry(_logger)).ToList();
        }
        catch (MySqlException e)
        {
            _logger?.LogError(GettingAllChatsFailed, e, "Getting all chats failed");
            throw;
        }
    }

    public async Task<List<GistWithFeed>> GetNextFiveGistsWithFeedAsync(int lastGistId, CancellationToken ct)
    {
        const string query = """
            SELECT
                g.Id as Id,
                g.Reference as Reference,
                f.Title as FeedTitle,
                f.RssUrl as FeedUrl,
                g.Title as Title,
                g.Author as Author,
                g.Url as Url,
                DATE_FORMAT(g.Published, '%Y-%m-%dT%H:%i:%s.%fZ') as Published,
                DATE_FORMAT(g.Updated, '%Y-%m-%dT%H:%i:%s.%fZ') as Updated,
                g.Summary as Summary,
                g.Tags as Tags,
                g.SearchQuery as SearchQuery
            FROM Gists g
            INNER JOIN Feeds f ON g.FeedId = f.Id
            WHERE g.Id > @LastGistId AND g.Disabled IS FALSE ORDER BY g.Id ASC LIMIT 5
        """;
        var command = new CommandDefinition(query, new { LastGistId = lastGistId }, cancellationToken: ct);

        try
        {
            await using var connection = await GetOpenConnectionAsync(ct);
            return (await connection.QueryAsync<GistWithFeed>(command).WithDeadlockRetry(_logger)).ToList();
        }
        catch (MySqlException e)
        {
            _logger?.LogError(GettingNextFiveGistsWithFeedFailed, e,
                "Getting next gists with feed with lastGistId {LastGistId} failed", lastGistId);
            throw;
        }
    }

    public async Task SetGistIdLastSentForChatAsync(long chatId, int gistId, CancellationToken ct)
    {
        const string query = "UPDATE Chats SET GistIdLastSent = @GistIdLastSent WHERE Id = @ChatId";
        var command = new CommandDefinition(query, new { GistIdLastSent = gistId, ChatId = chatId },
            cancellationToken: ct);

        try
        {
            await using var connection = await GetOpenConnectionAsync(ct);
            var rowsAffected = await connection.ExecuteAsync(command).WithDeadlockRetry(_logger);
            if (rowsAffected != 1)
                throw new DatabaseOperationException(
                    $"Did not successfully set GistIdLastSent for Chat {chatId} to {gistId}");
        }
        catch (MySqlException e)
        {
            _logger?.LogError(SettingGistIdLastSentFailed, e,
                "Setting GistIdLastSent for chat with ID {ChatId} to {GistId} failed", chatId, gistId);
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
        catch (Exception e)
        {
            _logger?.LogError(DatabaseConnectionFailed, e, "Failed to connect to database");
            if (connection is not null) await connection.DisposeAsync();
            throw;
        }
    }
}
