using System.Text.Json.Serialization;

namespace GistBackend.Types;

[method: JsonConstructor]
public record Gist(
    string Reference,
    int FeedId,
    string Author,
    bool IsSponsoredContent,
    DateTime Published,
    DateTime Updated,
    Uri Url,
    string Tags,
    int? Id = null
) {
    public int? Id { get; set; } = Id;

    public Gist(
        RssEntry entry,
        SummaryAIResponse summaryAIResponse,
        bool isSponsoredContent
    ) : this(
        entry.Reference,
        entry.FeedId,
        entry.Author,
        isSponsoredContent,
        entry.Published,
        entry.Updated,
        entry.Url,
        string.Join(";;", summaryAIResponse.Tags)
    )
    {
    }

    // Constructor for disabled gists
    public Gist(RssEntry entry) : this(
        entry.Reference,
        entry.FeedId,
        entry.Author,
        false,
        entry.Published,
        entry.Updated,
        entry.Url,
        ""
    )
    {
    }
}
