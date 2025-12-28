using System.Text.Json.Serialization;

namespace GistBackend.Types;

[method: JsonConstructor]
public record Gist(
    string Reference,
    int FeedId,
    string Author,
    DateTime Published,
    DateTime Updated,
    Uri Url,
    string Tags,
    int? Id = null
) {
    public int? Id { get; set; } = Id;

    public Gist(
        RssEntry entry,
        SummaryAIResponse summaryAIResponse
    ) : this(
        entry.Reference,
        entry.FeedId,
        entry.Author,
        entry.Published,
        entry.Updated,
        entry.Url,
        string.Join(";;", summaryAIResponse.Tags)
    )
    {
    }
}
