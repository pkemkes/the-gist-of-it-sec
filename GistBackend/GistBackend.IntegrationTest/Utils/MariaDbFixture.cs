using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;

namespace GistBackend.IntegrationTest.Utils;

public class MariaDbFixture : IAsyncLifetime {
    public const string GistServiceDbUsername = "gist_service_user";
    public const string GistServiceDbPassword = "gist_service_pass";
    public const string RecapServiceDbUsername = "recap_service_user";
    public const string RecapServiceDbPassword = "recap_service_pass";

    private readonly IFutureDockerImage _image;
    private readonly IContainer _container;

    public string Hostname => _container.Hostname;
    public uint ExposedPort => _container.GetMappedPublicPort(3306);

    public MariaDbFixture()
    {
        _image = new ImageFromDockerfileBuilder()
            .WithName("the-gist-of-it-sec-database-test")
            .WithDockerfileDirectory("../../../../../database")
            .WithCleanUp(true)
            .Build();

        _container = new ContainerBuilder()
            .WithImage(_image)
            .WithPortBinding(3306, true)
            .WithEnvironment("MARIADB_ROOT_PASSWORD", "root_pass")
            .WithEnvironment("DB_GISTSERVICE_USERNAME", GistServiceDbUsername)
            .WithEnvironment("DB_GISTSERVICE_PASSWORD", GistServiceDbPassword)
            .WithEnvironment("DB_RECAPSERVICE_USERNAME", RecapServiceDbUsername)
            .WithEnvironment("DB_RECAPSERVICE_PASSWORD", RecapServiceDbPassword)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(3306))
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _image.CreateAsync();
        await _container.StartAsync();
    }

    public async Task DisposeAsync() => await _container.StopAsync();
}
