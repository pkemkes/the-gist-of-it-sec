using GistBackend.Types;

namespace GistBackend.UnitTest.Utils;

public static class TestData
{
    public static readonly List<RssEntry> TestRssEntries = [
        new (
            "test reference",
            1,
            "test author",
            "test title",
            DateTime.UnixEpoch,
            DateTime.UnixEpoch,
            "test url",
            [ "test category" ],
            content => content
        ),
        new (
            "another test reference",
            1,
            "another test author",
            "another test title",
            DateTime.UnixEpoch.AddYears(30),
            DateTime.UnixEpoch.AddYears(30),
            "another test url",
            [ "another test category" ],
            content => content
        )
    ];

    public static readonly List<RssFeed> TestRssFeeds = [
        new("test url", content => content) {
            Id = 1,
            Title = "test title",
            Language = "test language",
            Entries = TestRssEntries
        },
        new("another test url", content => content) {
            Id = 2,
            Title = "another test title",
            Language = "another test language"
        }
    ];

    public static readonly List<string> TestTexts = [ "test text", "another test text" ];

    public static readonly List<SummaryAIResponse> TestAIResponses = [
        new("test summary", [ "test tag", "second tag" ], "test search query"),
        new("another test summary", [ "another test tag", "another second tag" ], "another test search query")
    ];

    public static readonly List<Gist> TestGists =
        TestRssEntries.Zip(TestAIResponses).Select((tuple, i) => new Gist(
            entry: tuple.First,
            summaryAIResponse: tuple.Second
        ) { Id = i }).ToList();
}
