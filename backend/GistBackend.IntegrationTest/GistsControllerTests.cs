using System.Net.Http.Json;
using System.Text.Json;
using GistBackend.Controllers;
using GistBackend.Handlers;
using GistBackend.Handlers.ChromaDbHandler;
using GistBackend.Handlers.MariaDbHandler;
using GistBackend.IntegrationTest.Utils;
using GistBackend.Types;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using static TestUtilities.TestData;

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

        var mariaDbHandlerOptions = new MariaDbHandlerOptions
        {
            Server = mariaDbFixture.Hostname,
            User = MariaDbFixture.GistServiceDbUsername,
            Password = MariaDbFixture.GistServiceDbPassword,
            Port = mariaDbFixture.ExposedPort
        };
        _mariaDbHandler = new MariaDbHandler(Options.Create(mariaDbHandlerOptions), new DateTimeHandler(), null);

        _chromaDbFixture = new ChromaDbFixture();
        _chromaDbFixture.InitializeAsync().GetAwaiter().GetResult();

        var chromeDbHandlerOptions = new ChromaDbHandlerOptions
        {
            Server = _chromaDbFixture.Hostname,
            ServerAuthnCredentials = ChromaDbFixture.GistServiceServerAuthnCredentials,
            Port = _chromaDbFixture.ExposedPort
        };
        _chromaDbHandler = new ChromaDbHandler(OpenAiHandlerUtils.CreateOpenAIHandlerMock(), new HttpClient(),
            Options.Create(chromeDbHandlerOptions), null);

        _factory = new WebApplicationFactory<StartUp>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    // Remove existing IMariaDbHandler registration
                    var mariaDbHandlerDescriptor = services.SingleOrDefault(d =>
                        d.ServiceKey?.Equals(StartUp.GistsControllerMariaDbHandlerOptionsName) == true);
                    if (mariaDbHandlerDescriptor != null) services.Remove(mariaDbHandlerDescriptor);

                    // Remove any hosted services that use IOptionsSnapshot<MariaDbHandlerOptions>
                    var hostedServiceDescriptors = services.Where(d =>
                        d.ServiceType == typeof(IHostedService)).ToList();
                    foreach (var d in hostedServiceDescriptors)
                    {
                        services.Remove(d);
                    }

                    // Register test mariaDbHandler
                    services.AddKeyedTransient<IMariaDbHandler>(StartUp.GistsControllerMariaDbHandlerOptionsName,
                        (_, _) => _mariaDbHandler);

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
        const Language feedLanguage = Language.De;
        var testFeed = (await _mariaDbHandler.InsertTestFeedInfosAsync(feedLanguage, 1)).Single();
        var testGists = await _mariaDbHandler.InsertTestGistsAsync(5, testFeed.Id);
        var testSummaries = await Task.WhenAll(testGists.Select(gist =>
                _mariaDbHandler.InsertTestSummariesAsync(gist.Id!.Value, feedLanguage)));
        var originalSummaries = testSummaries.Select(summaryPair => summaryPair.First());
        var expectedConstructedGists = testGists.Zip(originalSummaries,
            (gist, summary) => ConstructedGist.FromGistFeedAndSummary(gist, testFeed, summary)).ToList();

        var actual = await _client.GetAsync(RoutingConstants.GistsRoute);

        actual.EnsureSuccessStatusCode();
        var actualConstructedGist = await actual.Content.ReadFromJsonAsync<List<ConstructedGist>>(_jsonSerializerOptions);
        Assert.NotNull(actualConstructedGist);
        Assert.Equivalent(expectedConstructedGists, actualConstructedGist);
    }

    [Fact]
    public async Task GetGists_QueryWithParameters_CorrectGists()
    {
        var searchWords = new[] { "word1", "word2" };
        var tags = new[] { "tag1", "tag2" };
        var enabledFeed = (await _mariaDbHandler.InsertTestFeedInfosAsync(Language.De, 1)).Single();
        await _mariaDbHandler.InsertTestGistsAsync(4, enabledFeed.Id);  // gists of enabled feed
        var gistToFind = CreateTestGist(enabledFeed.Id) with {
            Tags = string.Join(";;", tags.Concat(_random.NextArrayOfStrings(3)))
        };
        gistToFind.Id = await _mariaDbHandler.InsertGistAsync(gistToFind, CancellationToken.None);
        var summary = CreateTestSummary(Language.En, true, gistToFind.Id) with {
            Title = $"This is a {searchWords.First()} title",
            SummaryText = $"This is a {searchWords.Last()} summary"
        };
        await _mariaDbHandler.InsertSummaryAsync(summary, CancellationToken.None);
        var expectedConstructedGist = ConstructedGist.FromGistFeedAndSummary(gistToFind, enabledFeed, summary);
        var otherGistsOfEnabledFeed = await _mariaDbHandler.InsertTestConstructedGistsAsync(5, enabledFeed);
        var disabledFeed = (await _mariaDbHandler.InsertTestFeedInfosAsync(Language.De, 1)).Single();
        await _mariaDbHandler.InsertTestConstructedGistsAsync(10, disabledFeed);  // gists of disabled feed
        var parameters = new Dictionary<string, string?> {
            { "lastGist", otherGistsOfEnabledFeed.Last().Id.ToString() },
            { "tags", string.Join(";;", tags) },
            { "q", string.Join("  ", searchWords) },
            { "disabledFeeds", string.Join(",", [ disabledFeed.Id, 1337, 4332, 4332 ]) },
            { "languageMode", nameof(LanguageMode.En) }
        };
        var uri = QueryHelpers.AddQueryString(RoutingConstants.GistsRoute, parameters);

        var actual = await _client.GetAsync(uri);

        actual.EnsureSuccessStatusCode();
        var actualGistsWithFeed = await actual.Content.ReadFromJsonAsync<List<ConstructedGist>>(_jsonSerializerOptions);
        Assert.NotNull(actualGistsWithFeed);
        var actualGistWithFeed = Assert.Single(actualGistsWithFeed);
        Assert.Equivalent(expectedConstructedGist, actualGistWithFeed);
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
        var testFeed = (await _mariaDbHandler.InsertTestFeedInfosAsync(Language.De, 1)).Single();
        var testGists = await _mariaDbHandler.InsertTestGistsAsync(3, testFeed.Id);
        var testConstructedGists = new List<ConstructedGist>();
        for (var i = 0; i < testGists.Count; i++)
        {
            var gist = testGists[i];
            var text = TestTextsAndEmbeddings.Keys.ElementAt(i);
            var entry = CreateTestEntry(gist.FeedId) with { Reference = gist.Reference };
            await _chromaDbHandler.UpsertEntryAsync(entry, text, CancellationToken.None);
            var summaries = await _mariaDbHandler.InsertTestSummariesAsync(gist.Id!.Value, testFeed.Language);
            testConstructedGists.Add(ConstructedGist.FromGistFeedAndSummary(gist, testFeed, summaries.First()));
        }
        var gistId = testConstructedGists.First().Id;
        var expectedConstructedGists = testConstructedGists.Where(gist => gist.Id != gistId).ToList();

        var actual = await _client.GetAsync($"{RoutingConstants.GistsRoute}/{gistId}/similar");

        actual.EnsureSuccessStatusCode();
        var similarGistsWithFeed =
            await actual.Content.ReadFromJsonAsync<List<SimilarGistWithFeed>>(_jsonSerializerOptions);
        Assert.NotNull(similarGistsWithFeed);
        var gistsWithFeed = similarGistsWithFeed.Select(similarGist => similarGist.Gist).ToList();
        Assert.Equivalent(expectedConstructedGists.OrderBy(g => g.Id), gistsWithFeed.OrderBy(g => g.Id));
    }

    [Fact]
    public async Task GetSimilarGistsAsync_OneDisabledFeed_OnlySimilarGistsOfOtherFeeds()
    {
        var enabledFeed = (await _mariaDbHandler.InsertTestFeedInfosAsync(Language.De, 1)).Single();
        var gistsOfEnabledFeed = await _mariaDbHandler.InsertTestGistsAsync(2, enabledFeed.Id);
        var disabledFeed = (await _mariaDbHandler.InsertTestFeedInfosAsync(Language.En, 1)).Single();
        var gistsOfDisabledFeed = await _mariaDbHandler.InsertTestGistsAsync(1, disabledFeed.Id);
        var testGists = gistsOfEnabledFeed.Concat(gistsOfDisabledFeed).ToList();
        var testConstructedGists = new List<ConstructedGist>();
        for (var i = 0; i < testGists.Count; i++)
        {
            var gist = testGists[i];
            var feed = gist.FeedId == enabledFeed.Id ? enabledFeed : disabledFeed;
            var text = TestTextsAndEmbeddings.Keys.ElementAt(i);
            var entry = CreateTestEntry(gist.FeedId) with { Reference = gist.Reference };
            await _chromaDbHandler.UpsertEntryAsync(entry, text, CancellationToken.None);
            var summaries = await _mariaDbHandler.InsertTestSummariesAsync(gist.Id!.Value, feed.Language);
            testConstructedGists.Add(ConstructedGist.FromGistFeedAndSummary(gist, feed, summaries.First()));
        }
        var gistId = testGists.First().Id!.Value;
        var expectedGistsWithFeed = testConstructedGists.Where(gist => gist.FeedTitle == enabledFeed.Title)
            .Where(gist => gist.Id != gistId).ToList();
        var parameters = new Dictionary<string, string?> {
            { "disabledFeeds", string.Join(",", [ disabledFeed.Id, 33, 83, 83 ]) }
        };
        var uri = QueryHelpers.AddQueryString($"{RoutingConstants.GistsRoute}/{gistId}/similar", parameters);

        var actual = await _client.GetAsync(uri);

        actual.EnsureSuccessStatusCode();
        var similarGistsWithFeed = await actual.Content.ReadFromJsonAsync<List<SimilarGistWithFeed>>(_jsonSerializerOptions);
        Assert.NotNull(similarGistsWithFeed);
        var gistsWithFeed = similarGistsWithFeed.Select(similarGist => similarGist.Gist);
        Assert.Equivalent(expectedGistsWithFeed, gistsWithFeed);
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
        var expectedFeeds = await _mariaDbHandler.InsertTestFeedInfosAsync(Language.De, 5);

        var actual = await _client.GetAsync($"{RoutingConstants.GistsRoute}/feeds");

        actual.EnsureSuccessStatusCode();
        var feeds = await actual.Content.ReadFromJsonAsync<List<RssFeedInfo>>(_jsonSerializerOptions);
        Assert.NotNull(feeds);
        Assert.Equal(expectedFeeds, feeds);
    }
}
