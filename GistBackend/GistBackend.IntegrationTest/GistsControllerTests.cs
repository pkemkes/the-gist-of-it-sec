using System.Net.Http.Json;
using System.Text.Json;
using GistBackend.Controllers;
using GistBackend.Handler;
using GistBackend.Handler.MariaDbHandler;
using GistBackend.IntegrationTest.Utils;
using GistBackend.Types;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using static GistBackend.IntegrationTest.Utils.TestData;

namespace GistBackend.IntegrationTest;

public class GistsControllerTests : IDisposable
{
    private readonly MariaDbFixture _fixture;
    private readonly WebApplicationFactory<StartUp> _factory;
    private readonly HttpClient _client;
    private readonly MariaDbHandler _dbHandler;
    private readonly Random _random = new();

    private readonly JsonSerializerOptions _jsonSerializerOptions = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public GistsControllerTests()
    {
        _fixture = new MariaDbFixture();
        _fixture.InitializeAsync().GetAwaiter().GetResult();

        var dbOptions = new MariaDbHandlerOptions(
            _fixture.Hostname,
            MariaDbFixture.GistServiceDbUsername,
            MariaDbFixture.GistServiceDbPassword,
            _fixture.ExposedPort
        );

        _factory = new WebApplicationFactory<StartUp>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    // Remove existing IMariaDbHandler registration
                    var dbHandlerDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IMariaDbHandler));
                    if (dbHandlerDescriptor != null) services.Remove(dbHandlerDescriptor);

                    // Remove any hosted services that use IOptionsSnapshot<MariaDbHandlerOptions>
                    var hostedServiceDescriptors = services.Where(d =>
                        d.ServiceType == typeof(IHostedService)).ToList();
                    foreach (var d in hostedServiceDescriptors)
                    {
                        services.Remove(d);
                    }

                    // Register test db handler
                    services.AddTransient<IMariaDbHandler>(sp =>
                        new MariaDbHandler(Options.Create(dbOptions), new DateTimeHandler(), null));
                });
            });

        _client = _factory.CreateClient();
        _dbHandler = new MariaDbHandler(Options.Create(dbOptions), new DateTimeHandler(), null);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        _fixture.Dispose();
    }

    [Fact]
    public async Task GetGists_NoGists_ReturnsEmptyArray()
    {
        var actual = await _client.GetAsync(RoutingConstants.GistsRoute);

        actual.EnsureSuccessStatusCode();
        var gists = await actual.Content.ReadFromJsonAsync<List<Gist>>(_jsonSerializerOptions);
        Assert.NotNull(gists);
        Assert.Empty(gists);
    }

    [Fact]
    public async Task GetGists_WithGists_ReturnsGists()
    {
        var expectedGists = await _dbHandler.InsertTestGistsAsync(5);

        var actual = await _client.GetAsync(RoutingConstants.GistsRoute);

        actual.EnsureSuccessStatusCode();
        var gists = await actual.Content.ReadFromJsonAsync<List<Gist>>(_jsonSerializerOptions);
        Assert.NotNull(gists);
        Assert.Equivalent(expectedGists, gists);
    }

    [Fact]
    public async Task GetGists_QueryWithParameters_ReturnsCorrectGists()
    {
        var searchWords = new[] { "word1", "word2" };
        var tags = new[] { "tag1", "tag2" };
        var gistsOfEnabledFeed = await _dbHandler.InsertTestGistsAsync(4);
        var enabledFeedId = gistsOfEnabledFeed.First().FeedId;
        var gistToFind = CreateTestGist(enabledFeedId) with {
            Title = $"This is a {searchWords.First()} title",
            Summary = $"This is a {searchWords.Last()} summary",
            Tags = string.Join(";;", tags.Concat(_random.NextArrayOfStrings(3)))
        };
        gistToFind.Id = await _dbHandler.InsertGistAsync(gistToFind, CancellationToken.None);
        var otherGistsOfEnabledFeed = await _dbHandler.InsertTestGistsAsync(5, enabledFeedId);
        var gistsOfDisabledFeed = await _dbHandler.InsertTestGistsAsync(10);
        var parameters = new Dictionary<string, string?> {
            { "lastGist", otherGistsOfEnabledFeed.Last().Id!.Value.ToString() },
            { "tags", string.Join(";;", tags) },
            { "q", string.Join("  ", searchWords) },
            { "disabledFeeds", string.Join(",", gistsOfDisabledFeed.Select(g => g.FeedId)) }
        };

        var actual = await _client.GetAsync(QueryHelpers.AddQueryString(RoutingConstants.GistsRoute, parameters));

        actual.EnsureSuccessStatusCode();
        var gists = await actual.Content.ReadFromJsonAsync<List<Gist>>(_jsonSerializerOptions);
        Assert.NotNull(gists);
        Assert.Single(gists);
        Assert.Equal(gists.Single(), gistToFind);
    }

    [Fact]
    public async Task GetHealth_AllHealthyButNoGistsInDb_ReturnsOk()
    {
        var actual = await _client.GetAsync($"{RoutingConstants.GistsRoute}/health");

        actual.EnsureSuccessStatusCode();
        Assert.Equal(System.Net.HttpStatusCode.OK, actual.StatusCode);
    }

    [Fact]
    public async Task GetHealth_AllHealthyAndSomeGistsInDb_ReturnsOk()
    {
        await _dbHandler.InsertTestGistsAsync(5);

        var actual = await _client.GetAsync($"{RoutingConstants.GistsRoute}/health");

        actual.EnsureSuccessStatusCode();
        Assert.Equal(System.Net.HttpStatusCode.OK, actual.StatusCode);
    }
}
