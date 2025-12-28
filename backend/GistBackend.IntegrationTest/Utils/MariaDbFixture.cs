using Dapper;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using GistBackend.Handlers.MariaDbHandler;
using MySqlConnector;

namespace GistBackend.IntegrationTest.Utils;

public class MariaDbFixture : IAsyncLifetime {
    public const string GistServiceDbUsername = "gist_service_user";
    public const string GistServiceDbPassword = "gist_service_pass";
    public const string RecapServiceDbUsername = "recap_service_user";
    public const string RecapServiceDbPassword = "recap_service_pass";
    public const string CleanupServiceDbUsername = "cleanup_service_user";
    public const string CleanupServiceDbPassword = "cleanup_service_pass";
    public const string GistsControllerDbUsername = "gists_controller_user";
    public const string GistsControllerDbPassword = "gists_controller_pass";
    public const string TelegramServiceDbUsername = "telegram_service_user";
    public const string TelegramServiceDbPassword = "telegram_service_pass";

    private readonly IFutureDockerImage _image;
    private readonly IContainer _container;

    public string Hostname => _container.Hostname;
    public uint ExposedPort => _container.GetMappedPublicPort(3306);
    private const string RootUser = "root";
    private const string RootPassword = "root_pass";

    public MariaDbFixture()
    {
        _image = new ImageFromDockerfileBuilder()
            .WithDockerfileDirectory("../../../../../database")
            .WithCleanUp(true)
            .Build();

        _container = new ContainerBuilder()
            .WithImage(_image)
            .WithPortBinding(3306, true)
            .WithEnvironment("MARIADB_ROOT_PASSWORD", RootPassword)
            .WithEnvironment("DB_GISTSERVICE_USERNAME", GistServiceDbUsername)
            .WithEnvironment("DB_GISTSERVICE_PASSWORD", GistServiceDbPassword)
            .WithEnvironment("DB_RECAPSERVICE_USERNAME", RecapServiceDbUsername)
            .WithEnvironment("DB_RECAPSERVICE_PASSWORD", RecapServiceDbPassword)
            .WithEnvironment("DB_CLEANUPSERVICE_USERNAME", CleanupServiceDbUsername)
            .WithEnvironment("DB_CLEANUPSERVICE_PASSWORD", CleanupServiceDbPassword)
            .WithEnvironment("DB_GISTSCONTROLLER_USERNAME", GistsControllerDbUsername)
            .WithEnvironment("DB_GISTSCONTROLLER_PASSWORD", GistsControllerDbPassword)
            .WithEnvironment("DB_TELEGRAMSERVICE_USERNAME", TelegramServiceDbUsername)
            .WithEnvironment("DB_TELEGRAMSERVICE_PASSWORD", TelegramServiceDbPassword)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(3306)
                .UntilCommandIsCompleted($"mariadb -u{RootUser} -p{RootPassword} -e 'SELECT 1'"))
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _image.CreateAsync();
        await _container.StartAsync();
    }

    public async Task ClearDatabaseAsync()
    {
        var options = new MariaDbHandlerOptions
        {
            Server = Hostname,
            User = RootUser,
            Password = RootPassword,
            Port = ExposedPort
        };
        await using var connection = new MySqlConnection(options.GetConnectionString());
        await connection.OpenAsync();
        const string query = """
            DELETE FROM RecapsDaily;
            DELETE FROM RecapsWeekly;
            DELETE FROM Chats;
            DELETE FROM Summaries;
            DELETE FROM Gists;
            DELETE FROM Feeds;
        """;
        await connection.ExecuteAsync(query);
    }

    public async Task DisposeAsync()
    {
        try
        {
            await _container.StopAsync();
        }
        finally
        {
            await _container.DisposeAsync();
        }
    }

    public void Dispose() => DisposeAsync().GetAwaiter().GetResult();
}
