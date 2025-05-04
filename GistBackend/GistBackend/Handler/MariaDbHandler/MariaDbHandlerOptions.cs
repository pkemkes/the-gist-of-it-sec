namespace GistBackend.Handler;

public record MariaDbHandlerOptions(
    string Server,
    string User,
    string Password,
    uint Port = 3306
) {
    public readonly string Database = "TheGistOfItSec";
};
