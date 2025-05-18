using GistBackend.Types;

namespace GistBackend.IntegrationTest.Utils;

public static class TestData
{
    private static readonly Random Random = new();

    public static RssFeedInfo CreateTestFeedInfo() => new(
        Random.NextString(),
        Random.NextString(),
        Random.NextString()
    );

    public static Gist CreateTestGist(int feedId) => new(
        Random.NextString(),
        feedId,
        Random.NextString(),
        Random.NextString(),
        Random.NextDateTime(max: DateTime.UnixEpoch.AddYears(30)),
        Random.NextDateTime(min: DateTime.UnixEpoch.AddYears(30)),
        Random.NextString(),
        Random.NextString(),
        string.Join(";;", Random.NextArrayOfStrings()),
        Random.NextString()
    );

    public static GoogleSearchResult CreateTestSearchResult(int gistId) => new(
        gistId,
        Random.NextString(),
        Random.NextString(),
        Random.NextString(),
        Random.NextString(),
        Random.NextString()
    );

    public static List<CategoryRecap> CreateTestRecap() => Enumerable.Range(0, 5).Select(_ =>
        new CategoryRecap(
            Random.NextString(),
            Random.NextString(),
            Enumerable.Range(0, 3).Select(_ => Random.Next(10000))
        )
    ).ToList();
}
