using MySqlConnector;

namespace GistBackend.Handlers.MariaDbHandler;

public record MariaDbHandlerOptions {
    public string Server { get; init; } = string.Empty;
    public string User { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public uint Port { get; init; } = 3306;
    public string Database { get; init; } = "TheGistOfItSec";

    public string GetConnectionString()
    {
        CheckIfConfigIsSet();
        return new MySqlConnectionStringBuilder {
            Server = Server,
            Port = Port,
            Database = Database,
            UserID = User,
            Password = Password
        }.ConnectionString;
    }

    private void CheckIfConfigIsSet()
    {
        if (string.IsNullOrWhiteSpace(Server) || string.IsNullOrWhiteSpace(User) || string.IsNullOrWhiteSpace(Password))
            throw new InvalidOperationException("MariaDB connection parameters are not set.");
    }
}
