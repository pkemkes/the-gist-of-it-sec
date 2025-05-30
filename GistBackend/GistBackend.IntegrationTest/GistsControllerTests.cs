using System.Net.Http.Json;
using System.Text.Json;
using GistBackend.Controllers;
using GistBackend.Handler;
using GistBackend.Handler.ChromaDbHandler;
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

[Collection(nameof(TestsWithoutParallelizationCollection))]
public class GistsControllerTests : IDisposable, IClassFixture<MariaDbFixture>
{
    private readonly MariaDbHandler _mariaDbHandler;
    private readonly ChromaDbFixture _chromaDbFixture;
    private readonly IChromaDbHandler _chromaDbHandler;
    private readonly WebApplicationFactory<StartUp> _factory;
    private readonly HttpClient _client;
    private readonly Random _random = new();

    private readonly JsonSerializerOptions _jsonSerializerOptions = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public GistsControllerTests(MariaDbFixture mariaDbFixture)
    {
        mariaDbFixture.ClearDatabaseAsync().GetAwaiter().GetResult();

        var mariaDbHandlerOptions = new MariaDbHandlerOptions(
            mariaDbFixture.Hostname,
            MariaDbFixture.GistServiceDbUsername,
            MariaDbFixture.GistServiceDbPassword,
            mariaDbFixture.ExposedPort
        );
        _mariaDbHandler = new MariaDbHandler(Options.Create(mariaDbHandlerOptions), new DateTimeHandler(), null);

        _chromaDbFixture = new ChromaDbFixture();
        _chromaDbFixture.InitializeAsync().GetAwaiter().GetResult();

        var chromeDbHandlerOptions = new ChromaDbHandlerOptions(
            _chromaDbFixture.Hostname,
            ChromaDbFixture.GistServiceServerAuthnCredentials,
            _chromaDbFixture.ExposedPort
        );
        _chromaDbHandler = new ChromaDbHandler(OpenAiHandlerUtils.CreateOpenAIHandlerMock(), new HttpClient(),
            Options.Create(chromeDbHandlerOptions), null);

        _factory = new WebApplicationFactory<StartUp>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    // Remove existing IMariaDbHandler registration
                    var mariaDbHandlerDescriptor =
                        services.SingleOrDefault(d => d.ServiceType == typeof(IMariaDbHandler));
                    if (mariaDbHandlerDescriptor != null) services.Remove(mariaDbHandlerDescriptor);

                    // Remove any hosted services that use IOptionsSnapshot<MariaDbHandlerOptions>
                    var hostedServiceDescriptors = services.Where(d =>
                        d.ServiceType == typeof(IHostedService)).ToList();
                    foreach (var d in hostedServiceDescriptors)
                    {
                        services.Remove(d);
                    }

                    // Register test mariaDbHandler
                    services.AddTransient<IMariaDbHandler>(_ => _mariaDbHandler);

                    // Remove existing IChromaDbHandler registration
                    var chromaDbHandlerDescriptor =
                        services.SingleOrDefault(d => d.ServiceType == typeof(IChromaDbHandler));
                    if (chromaDbHandlerDescriptor != null) services.Remove(chromaDbHandlerDescriptor);

                    // Register test chromaDbHandler
                    services.AddTransient<IChromaDbHandler>(_ => _chromaDbHandler);
                });
            });

        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        _chromaDbFixture.Dispose();
    }

    [Fact]
    public async Task GetGists_NoGists_EmptyArray()
    {
        var actual = await _client.GetAsync(RoutingConstants.GistsRoute);

        actual.EnsureSuccessStatusCode();
        var gists = await actual.Content.ReadFromJsonAsync<List<Gist>>(_jsonSerializerOptions);
        Assert.NotNull(gists);
        Assert.Empty(gists);
    }

    [Fact]
    public async Task GetGists_WithGists_Gists()
    {
        var expectedGists = await _mariaDbHandler.InsertTestGistsAsync(5);

        var actual = await _client.GetAsync(RoutingConstants.GistsRoute);

        actual.EnsureSuccessStatusCode();
        var gists = await actual.Content.ReadFromJsonAsync<List<Gist>>(_jsonSerializerOptions);
        Assert.NotNull(gists);
        Assert.Equivalent(expectedGists, gists);
    }

    [Fact]
    public async Task GetGists_QueryWithParameters_CorrectGists()
    {
        var searchWords = new[] { "word1", "word2" };
        var tags = new[] { "tag1", "tag2" };
        var gistsOfEnabledFeed = await _mariaDbHandler.InsertTestGistsAsync(4);
        var enabledFeedId = gistsOfEnabledFeed.First().FeedId;
        var gistToFind = CreateTestGist(enabledFeedId) with {
            Title = $"This is a {searchWords.First()} title",
            Summary = $"This is a {searchWords.Last()} summary",
            Tags = string.Join(";;", tags.Concat(_random.NextArrayOfStrings(3)))
        };
        gistToFind.Id = await _mariaDbHandler.InsertGistAsync(gistToFind, CancellationToken.None);
        var otherGistsOfEnabledFeed = await _mariaDbHandler.InsertTestGistsAsync(5, enabledFeedId);
        var gistsOfDisabledFeed = await _mariaDbHandler.InsertTestGistsAsync(10);
        var parameters = new Dictionary<string, string?> {
            { "lastGist", otherGistsOfEnabledFeed.Last().Id!.Value.ToString() },
            { "tags", string.Join(";;", tags) },
            { "q", string.Join("  ", searchWords) },
            { "disabledFeeds", string.Join(",", [ gistsOfDisabledFeed.First().FeedId, 1337, 4332, 4332 ]) }
        };
        var uri = QueryHelpers.AddQueryString(RoutingConstants.GistsRoute, parameters);

        var actual = await _client.GetAsync(uri);

        actual.EnsureSuccessStatusCode();
        var gists = await actual.Content.ReadFromJsonAsync<List<Gist>>(_jsonSerializerOptions);
        Assert.NotNull(gists);
        Assert.Single(gists);
        Assert.Equal(gistToFind, gists.Single());
    }

    [Fact]
    public async Task GetHealth_AllHealthyButNoGistsInDb_Ok()
    {
        var actual = await _client.GetAsync($"{RoutingConstants.GistsRoute}/health");

        actual.EnsureSuccessStatusCode();
        Assert.Equal(System.Net.HttpStatusCode.OK, actual.StatusCode);
    }

    [Fact]
    public async Task GetHealth_AllHealthyAndSomeGistsInDb_Ok()
    {
        await _mariaDbHandler.InsertTestGistsAsync(5);

        var actual = await _client.GetAsync($"{RoutingConstants.GistsRoute}/health");

        actual.EnsureSuccessStatusCode();
        Assert.Equal(System.Net.HttpStatusCode.OK, actual.StatusCode);
    }

    [Fact]
    public async Task GetSimilarGistsAsync_GistIdNotInDb_NotFound()
    {
        var actual = await _client.GetAsync($"{RoutingConstants.GistsRoute}/999999999/similar");

        Assert.Equal(System.Net.HttpStatusCode.NotFound, actual.StatusCode);
    }

    [Fact]
    public async Task GetSimilarGistsAsync_NoDisabledFeeds_AllSimilarGists()
    {
        var testGists = (await _mariaDbHandler.InsertTestGistsAsync(2))
            .Concat(await _mariaDbHandler.InsertTestGistsAsync(1)).ToList();
        for (var i = 0; i < testGists.Count; i++)
        {
            var text = TestTextsAndEmbeddings.Keys.ElementAt(i);
            var entry = CreateTestEntry() with { Reference = testGists[i].Reference };
            await _chromaDbHandler.InsertEntryAsync(entry, text, CancellationToken.None);
        }
        var gistId = testGists.First().Id!.Value;
        var expectedGists = testGists.Where(gist => gist.Id != gistId).ToList();

        var actual = await _client.GetAsync($"{RoutingConstants.GistsRoute}/{gistId}/similar");

        actual.EnsureSuccessStatusCode();
        var similarGists = await actual.Content.ReadFromJsonAsync<List<SimilarGist>>(_jsonSerializerOptions);
        Assert.NotNull(similarGists);
        var gists = similarGists.Select(similarGist => similarGist.Gist);
        Assert.Equivalent(expectedGists, gists);
    }

    [Fact]
    public async Task GetSimilarGistsAsync_OneDisabledFeed_OnlySimilarGistsOfOtherFeeds()
    {
        var gistsOfEnabledFeed = await _mariaDbHandler.InsertTestGistsAsync(2);
        var gistsOfDisabledFeed = await _mariaDbHandler.InsertTestGistsAsync(1);
        var testGists = gistsOfEnabledFeed.Concat(gistsOfDisabledFeed).ToList();
        for (var i = 0; i < testGists.Count; i++)
        {
            var text = TestTextsAndEmbeddings.Keys.ElementAt(i);
            var entry = CreateTestEntry() with { Reference = testGists[i].Reference };
            await _chromaDbHandler.InsertEntryAsync(entry, text, CancellationToken.None);
        }
        var gistId = testGists.First().Id!.Value;
        var expectedGists = gistsOfEnabledFeed.Where(gist => gist.Id != gistId).ToList();
        var parameters = new Dictionary<string, string?> {
            { "disabledFeeds", string.Join(",", [ gistsOfDisabledFeed.First().FeedId, 33, 83, 83 ]) }
        };
        var uri = QueryHelpers.AddQueryString($"{RoutingConstants.GistsRoute}/{gistId}/similar", parameters);

        var actual = await _client.GetAsync(uri);

        actual.EnsureSuccessStatusCode();
        var similarGists = await actual.Content.ReadFromJsonAsync<List<SimilarGist>>(_jsonSerializerOptions);
        Assert.NotNull(similarGists);
        var gists = similarGists.Select(similarGist => similarGist.Gist);
        Assert.Equivalent(expectedGists, gists);
    }

    [Fact]
    public async Task GetSearchResultsAsync_GistIdNotInDb_EmptyList()
    {
        var actual = await _client.GetAsync($"{RoutingConstants.GistsRoute}/999999999/searchResults");

        actual.EnsureSuccessStatusCode();
        var searchResults = await actual.Content.ReadFromJsonAsync<List<GoogleSearchResult>>(_jsonSerializerOptions);
        Assert.NotNull(searchResults);
        Assert.Empty(searchResults);
    }

    [Fact]
    public async Task GetSearchResultsAsync_GistIdInDb_SearchResults()
    {
        var gist = (await _mariaDbHandler.InsertTestGistsAsync(1)).Single();
        var expectedSearchResults = await _mariaDbHandler.InsertTestSearchResultsAsync(10, gist.Id!.Value);

        var actual = await _client.GetAsync($"{RoutingConstants.GistsRoute}/{gist.Id}/searchResults");

        actual.EnsureSuccessStatusCode();
        var searchResults = await actual.Content.ReadFromJsonAsync<List<GoogleSearchResult>>(_jsonSerializerOptions);
        Assert.NotNull(searchResults);
        Assert.Equivalent(expectedSearchResults, searchResults);
    }

    [Fact]
    public async Task GetAllFeedsAsync_NoFeedsInDb_EmptyList()
    {
        var actual = await _client.GetAsync($"{RoutingConstants.GistsRoute}/feeds");

        actual.EnsureSuccessStatusCode();
        var feeds = await actual.Content.ReadFromJsonAsync<List<RssFeedInfo>>(_jsonSerializerOptions);
        Assert.NotNull(feeds);
        Assert.Empty(feeds);
    }

    [Fact]
    public async Task GetAllFeedsAsync_FeedsInDb_Feeds()
    {
        var expectedFeeds = await _mariaDbHandler.InsertTestFeedInfosAsync(5);

        var actual = await _client.GetAsync($"{RoutingConstants.GistsRoute}/feeds");

        actual.EnsureSuccessStatusCode();
        var feeds = await actual.Content.ReadFromJsonAsync<List<RssFeedInfo>>(_jsonSerializerOptions);
        Assert.NotNull(feeds);
        Assert.Equivalent(expectedFeeds, feeds);
    }
}
