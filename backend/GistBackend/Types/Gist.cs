using System.Text.Json.Serialization;

namespace GistBackend.Types;

[method: JsonConstructor]
public record Gist(
    string Reference,
    int FeedId,
    string Author,
    string Title,
    DateTime Published,
    DateTime Updated,
    Uri Url,
    string Summary,
    string Tags,
    string SearchQuery,
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
        entry.Title,
        entry.Published,
        entry.Updated,
        entry.Url,
        summaryAIResponse.Summary,
        string.Join(";;", summaryAIResponse.Tags),
        summaryAIResponse.SearchQuery
    )
    {
    }
}
