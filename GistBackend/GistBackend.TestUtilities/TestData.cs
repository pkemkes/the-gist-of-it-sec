using GistBackend.IntegrationTest.Utils;
using GistBackend.Types;

namespace TestUtilities;

public static class TestData
{
    private static readonly Random Random = new();

    public static RssFeed CreateTestRssFeed()
    {
        var feedId = Random.Next();
        return new RssFeed(Random.NextString(), s => s) {
            Id = feedId,
            Title = Random.NextString(),
            Language = Random.NextString(),
            Entries = CreateTestEntries(5, feedId)
        };
    }

    public static RssFeedInfo CreateTestFeedInfo() => CreateTestRssFeed().ToRssFeedInfo();

    public static List<RssFeed> CreateTestRssFeeds(int count) =>
        Enumerable.Range(0, count).Select(_ => CreateTestRssFeed()).ToList();

    public static Gist CreateTestGist(int? feedId = null, string? reference = null, DateTime? updated = null) => new(
        reference ?? Random.NextString(),
        feedId ?? Random.Next(),
        Random.NextString(),
        Random.NextString(),
        Random.NextDateTime(max: DateTime.UnixEpoch.AddYears(30)),
        updated ?? Random.NextDateTime(min: DateTime.UnixEpoch.AddYears(30)),
        Random.NextString(),
        Random.NextString(),
        string.Join(";;", Random.NextArrayOfStrings()),
        Random.NextString(),
        Random.Next()
    );

    public static List<Gist> CreateTestGists(int count, int? feedId = null) =>
        Enumerable.Range(0, count).Select(_ => CreateTestGist(feedId ?? Random.Next())).ToList();

    public static GoogleSearchResult CreateTestSearchResult(int? gistId = null) => new(
        gistId ?? Random.Next(),
        Random.NextString(),
        Random.NextString(),
        Random.NextString(),
        Random.NextString(),
        Random.NextString()
    );

public static List<GoogleSearchResult> CreateTestSearchResults(int count, int? gistId = null) =>
        Enumerable.Range(0, count).Select(_ => CreateTestSearchResult(gistId)).ToList();

    public static List<CategoryRecap> CreateTestRecap() => Enumerable.Range(0, 5).Select(_ =>
        new CategoryRecap(
            Random.NextString(),
            Random.NextString(),
            Enumerable.Range(0, 3).Select(_ => Random.Next(10000))
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
        Random.NextString(),
        [Random.NextString(), Random.NextString(), Random.NextString()],
        text => text
    );

    public static List<string> CreateTestStrings(int count) =>
        Enumerable.Range(0, count).Select(_ => Random.NextString()).ToList();

    public static List<RssEntry> CreateTestEntries(int count, int? feedId = null) =>
        Enumerable.Range(0, count).Select(_ => CreateTestEntry(feedId)).ToList();

    public static List<SummaryAIResponse> CreateTestSummaryAIResponses(int count) =>
        Enumerable.Range(0, count).Select(_ => new SummaryAIResponse(
            Random.NextString(),
            CreateTestStrings(5),
            Random.NextString()
        )).ToList();

    public static List<CategoryRecap> CreateTestRecap(int categoryCount) =>
        Enumerable.Range(0, categoryCount).Select(_ =>
            new CategoryRecap(
                Random.NextString(),
                Random.NextString(),
                Enumerable.Range(0, Random.Next(3, 6)).Select(_ => Random.Next())
            )
        ).ToList();
}
