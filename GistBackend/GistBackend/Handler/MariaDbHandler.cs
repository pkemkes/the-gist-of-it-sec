using GistBackend.Types;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MySqlConnector;

namespace GistBackend.Handler;

public record MariaDbHandlerOptions(
    string Server,
    string Database,
    string User,
    string Password
);

public interface IMariaDbHandler {
    public Task<DateTimeOffset?> GetGistUpdatedByReferenceIfExistsAsync(string reference, CancellationToken ct);
    public Task UpsertGistAsync(Gist gist, bool olderGistExists, CancellationToken ct);
}

public class MariaDbHandler(IOptions<MariaDbHandlerOptions> options, ILogger<MariaDbHandler> logger) {
    private readonly string _connectionString = new MySqlConnectionStringBuilder {
        Server = options.Value.Server,
        Database = options.Value.Database,
        UserID = options.Value.User,
        Password = options.Value.Password
    }.ConnectionString;

    private MySqlConnection GetConnection() => new(_connectionString);

    public async Task<DateTimeOffset?> GetGistUpdatedByReferenceIfExistsAsync(string reference, CancellationToken ct)
    {
        const string query = "SELECT updated FROM gists WHERE reference = @reference";

        await using var connection = GetConnection();
        await connection.OpenAsync(ct);

        await using var command = new MySqlCommand(query, connection);
        command.Parameters.AddWithValue("@reference", reference);

        await using var reader = await command.ExecuteReaderAsync(ct);

        if (!reader.HasRows) return null;
        DateTimeOffset? result = null;
        while (await reader.ReadAsync(ct))
        {
            if (result is not null) throw new Exception($"Found multiple gists with reference {reference}");
            result = reader.GetDateTimeOffset(0);
        }

        return result;
    }

    public Task UpsertGistAsync(Gist gist, bool olderGistExists, CancellationToken ct) =>
        olderGistExists ? InsertGistAsync(gist, ct) : UpdateGistAsync(gist, ct);

    private async Task InsertGistAsync(Gist gist, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    private async Task UpdateGistAsync(Gist gist, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}
