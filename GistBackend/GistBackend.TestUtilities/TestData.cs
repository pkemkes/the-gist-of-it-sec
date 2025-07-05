using GistBackend.IntegrationTest.Utils;
using GistBackend.Types;

namespace TestUtilities;

public static class TestData
{
    private static readonly Random Random = new();

    public static RssFeed CreateTestRssFeed() => new(Random.NextString(), s => s) {
        Id = Random.Next(),
        Title = Random.NextString(),
        Language = Random.NextString(),
        Entries = CreateTestEntries(5)
    };

    public static List<RssFeed> CreateTestRssFeeds(int count) =>
        Enumerable.Range(0, count).Select(_ => CreateTestRssFeed()).ToList();

    public static RssFeedInfo CreateTestFeedInfo() => CreateTestRssFeed().ToRssFeedInfo();

    public static Gist CreateTestGist(int? feedId = null) => new(
        Random.NextString(),
        feedId ?? Random.Next(),
        Random.NextString(),
        Random.NextString(),
        Random.NextDateTime(max: DateTime.UnixEpoch.AddYears(30)),
        Random.NextDateTime(min: DateTime.UnixEpoch.AddYears(30)),
        Random.NextString(),
        Random.NextString(),
        string.Join(";;", Random.NextArrayOfStrings()),
        Random.NextString(),
        Random.Next()
    );

    public static List<Gist> CreateTestGists(int count, int? feedId = null) =>
        Enumerable.Range(0, count).Select(_ => CreateTestGist(feedId ?? Random.Next())).ToList();

    public static GoogleSearchResult CreateTestSearchResult(int gistId) => new(
        gistId,
        Random.NextString(),
        Random.NextString(),
        Random.NextString(),
        Random.NextString(),
        Random.NextString()
    );

public static List<GoogleSearchResult> CreateTestSearchResults(int count, int gistId) =>
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

    public static RssEntry CreateTestEntry() => new(
        Random.NextString(),
        Random.Next(),
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

    public static List<RssEntry> CreateTestEntries(int count) =>
        Enumerable.Range(0, count).Select(_ => CreateTestEntry()).ToList();

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
