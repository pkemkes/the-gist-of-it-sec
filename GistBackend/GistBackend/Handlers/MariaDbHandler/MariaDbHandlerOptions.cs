using MySqlConnector;

namespace GistBackend.Handlers.MariaDbHandler;

public record MariaDbHandlerOptions(
    string Server,
    string User,
    string Password,
    uint Port = 3306
) {
    public readonly string Database = "TheGistOfItSec";

    public string GetConnectionString() =>
        new MySqlConnectionStringBuilder {
            Server = Server,
            Port = Port,
            Database = Database,
            UserID = User,
            Password = Password
        }.ConnectionString;
};
