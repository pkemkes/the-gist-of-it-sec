namespace GistBackend.Types;

public record Gist(
    string Reference,
    int FeedId,
    string Author,
    string Title,
    DateTime Published,
    DateTime Updated,
    string Url,
    string Summary,
    string Tags,
    string SearchQuery,
    int? Id = null
) {
    public int? Id { get; set; } = Id;

    public Gist(
        RssEntry entry,
        AIResponse aiResponse
    ) : this(
        entry.Reference,
        entry.FeedId,
        entry.Author,
        entry.Title,
        entry.Published,
        entry.Updated,
        entry.Url,
        aiResponse.Summary,
        string.Join(";;", aiResponse.Tags),
        aiResponse.SearchQuery
    )
    {
    }
}
