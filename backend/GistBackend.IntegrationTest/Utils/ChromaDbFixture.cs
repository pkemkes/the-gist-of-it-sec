using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;

namespace GistBackend.IntegrationTest.Utils;

public class ChromaDbFixture : IAsyncLifetime
{
    public const string GistServiceServerAuthnCredentials = "gist_service_user";

    private readonly IFutureDockerImage _image;
    private readonly IContainer _container;

    public string Hostname => _container.Hostname;
    public uint ExposedPort => _container.GetMappedPublicPort(8000);

    public ChromaDbFixture()
    {
        _image = new ImageFromDockerfileBuilder()
            .WithName("the-gist-of-it-sec-chromadb-test")
            .WithDockerfileDirectory("../../../../../chromadb")
            .WithCleanUp(true)
            .Build();

        _container = new ContainerBuilder()
            .WithImage(_image)
            .WithPortBinding(8000, true)
            .WithEnvironment("CHROMA_SERVER_AUTHN_CREDENTIALS", GistServiceServerAuthnCredentials)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(8000))
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _image.CreateAsync();
        await _container.StartAsync();
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
