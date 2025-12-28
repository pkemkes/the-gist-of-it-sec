using System.Net;
using System.ServiceModel.Syndication;
using GistBackend.Handlers;
using GistBackend.IntegrationTest.Utils;
using GistBackend.Types;

namespace TestUtilities;

public static class TestData
{
    private static readonly Random Random = new();

    public static RssFeed CreateTestRssFeed(Language language) => new(Random.NextUri(), s => s, language);

    public static List<TestFeedData> CreateTestFeeds(int count = 5) =>
        Enumerable.Range(0, count).Select(_ => new TestFeedData()).ToList();

    public static RssFeedInfo CreateTestFeedInfo(Language language) => new(
        Random.NextString(),
        Random.NextUri(),
        language
    );

    public static RssFeedInfo CreateTestFeedInfo() => CreateTestFeedInfo(Language.De);

    public static List<RssFeed> CreateTestRssFeeds(int count, Language language) =>
        Enumerable.Range(0, count).Select(_ => CreateTestRssFeed(language)).ToList();

    public static SyndicationFeed CreateTestSyndicationFeed(List<RssEntry>? entries = null)
    {
        entries ??= CreateTestEntries(5);
        return new SyndicationFeed(Random.NextString(), Random.NextString(), Random.NextUri())
            {
                Items = entries.Select(entry => {
                    var item = new SyndicationItem(
                        entry.Title,
                        Random.NextString(),
                        entry.Url,
                        entry.Reference,
                        entry.Updated
                    ) {
                        PublishDate = entry.Published
                    };
                    item.Authors.Add(new SyndicationPerson(entry.Author){ Name = entry.Author });
                    foreach (var category in entry.Categories)
                    {
                        item.Categories.Add(new SyndicationCategory(category));
                    }
                    return item;
                }).ToList(),
                Language = Random.NextString(),
            };
    }

    public static Gist CreateTestGist(int? feedId = null, string? reference = null, DateTime? updated = null) => new(
        reference ?? Random.NextString(),
        feedId ?? Random.Next(),
        Random.NextString(),
        Random.NextDateTime(max: DateTime.UnixEpoch.AddYears(30)),
        updated ?? Random.NextDateTime(min: DateTime.UnixEpoch.AddYears(30)),
        Random.NextUri(),
        string.Join(";;", Random.NextArrayOfStrings()),
        Random.Next()
    );

    public static List<Gist> CreateTestGists(int count, int? feedId = null) =>
        Enumerable.Range(0, count).Select(_ => CreateTestGist(feedId ?? Random.Next())).ToList();

    public static Gist CreateTestGistFromEntry(RssEntry entry, SummaryAIResponse? summaryAIResponse = null)
    {
        summaryAIResponse ??= CreateTestSummaryAIResponse();
        return new Gist(
            entry.Reference,
            entry.FeedId,
            entry.Author,
            entry.Published,
            entry.Updated,
            entry.Url,
            string.Join(";;", summaryAIResponse.Tags),
            Random.Next()
        );
    }

    public static Summary CreateTestSummary(Language language, bool isTranslated, int? gistId = null) => new(
        gistId ?? Random.Next(),
        language,
        isTranslated,
        Random.NextString(),
        Random.NextString()
    );

    public static List<ConstructedGist> CreateTestConstructedGists(int count, RssFeedInfo? feed = null,
        Language language = Language.De)
    {
        feed ??= CreateTestFeedInfo(language);
        return Enumerable.Range(0, count).Select(_ => CreateTestConstructedGist(feed)).ToList();
    }

    public static ConstructedGist CreateTestConstructedGist(RssFeedInfo? feed = null, string? reference = null,
        DateTime? updated = null, Language language = Language.De, LanguageMode languageMode = LanguageMode.Original)
    {
        feed ??= CreateTestFeedInfo(language);
        var gist = CreateTestGist(feed.Id, reference, updated);
        var isTranslated = languageMode == LanguageMode.Original ||
                           languageMode == LanguageMode.De && language == Language.De ||
                           languageMode == LanguageMode.En && language == Language.En;
        var summary = CreateTestSummary(language, isTranslated, gist.Id);
        return ConstructedGist.FromGistFeedAndSummary(gist, feed, summary);
    }

    public static RecapAIResponse CreateTestRecap() => new(CreateTestRecapSections(), CreateTestRecapSections());

    private static List<RecapSection> CreateTestRecapSections() =>
        Enumerable.Range(0, 5).Select(_ =>
            new RecapSection(
                Random.NextString(),
                Random.NextString(),
                Enumerable.Range(0, 3).Select(_ => Random.Next(10000)).ToList()
            )
        ).ToList();

    public static readonly Dictionary<string, float[]> TestTextsAndEmbeddings = new() {
        { "test text", Enumerable.Repeat(0.1f, 100).ToArray() },
        { "very different test text", Enumerable.Repeat(0.9f, 100).ToArray() },
        { "very similar test text", Enumerable.Repeat(0.100000001f, 100).ToArray() },
    };

    public static RssEntry CreateTestEntry(int? feedId = null) => new(
        Random.NextString(),
        feedId ?? Random.Next(),
        Random.NextString(),
        Random.NextString(),
        Random.NextDateTime(max: DateTime.UnixEpoch.AddYears(30)),
        Random.NextDateTime(min: DateTime.UnixEpoch.AddYears(30)),
        Random.NextUri(),
        [Random.NextString(), Random.NextString(), Random.NextString()],
        text => text
    );

    public static List<string> CreateTestStrings(int count) =>
        Enumerable.Range(0, count).Select(_ => Random.NextString()).ToList();

    public static List<RssEntry> CreateTestEntries(int count, int? feedId = null) =>
        Enumerable.Range(0, count).Select(_ => CreateTestEntry(feedId)).ToList();

    private static SummaryAIResponse CreateTestSummaryAIResponse() => new(
        Random.NextString(),
        Random.NextString(),
        Random.NextString(),
        CreateTestStrings(Random.Next(1, 5))
    );

    public static List<SummaryAIResponse> CreateTestSummaryAIResponses(int count) =>
        Enumerable.Range(0, count).Select(_ => CreateTestSummaryAIResponse()).ToList();

    // Helper for mocking HttpClient
    private class MockHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
            => sendAsync(request, cancellationToken);
    }

    public static HttpClient CreateMockedHttpClient(List<TestFeedData> testFeeds)
    {
        var syndicationXmlByRssUrl = testFeeds.ToDictionary(
            feed => feed.RssFeed.RssUrl,
            feed => feed.SyndicationFeedXml
        );
        var httpMessageHandlerMock = new MockHttpMessageHandler((request, _) =>
        {
            if (request.RequestUri is not null &&
                syndicationXmlByRssUrl.TryGetValue(request.RequestUri, out var responseContent))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(responseContent)
                });
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });
        return new HttpClient(httpMessageHandlerMock);
    }

    public static RssFeedHandler CreateRssFeedHandler(HttpClient httpClient, List<TestFeedData> testFeeds) =>
        new(httpClient) { Definitions = testFeeds.Select(f => f.RssFeed).ToList() };
}
