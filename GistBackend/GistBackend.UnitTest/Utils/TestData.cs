using GistBackend.Types;

namespace GistBackend.UnitTest.Utils;

public static class TestData
{
    public static readonly List<RssEntry> TestRssEntries = [
        new (
            "first test reference",
            1,
            "first test author",
            "first test title",
            DateTime.UnixEpoch,
            DateTime.UnixEpoch,
            "first test url",
            [ "first test category" ],
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
        new("first test feed url", content => content) {
            Id = 1,
            Title = "first test feed title",
            Language = "first test feed language",
            Entries = TestRssEntries
        },
        new("another test feed url", content => content) {
            Id = 2,
            Title = "another test feed title",
            Language = "another test feed language"
        }
    ];

    public static readonly List<string> TestTexts = [ "first test text", "another test text" ];

    public static readonly List<SummaryAIResponse> TestAIResponses = [
        new("first test summary", [ "first test tag", "first second tag" ], "first test search query"),
        new("another test summary", [ "another test tag", "another second tag" ], "another test search query")
    ];

    public static readonly List<Gist> TestGists =
        TestRssEntries.Zip(TestAIResponses).Select((tuple, i) => new Gist(
            entry: tuple.First,
            summaryAIResponse: tuple.Second
        ) { Id = i }).ToList();
}
