namespace GistBackend.Types;

public record Gist(
    string Reference,
    int FeedId,
    string Author,
    string Title,
    DateTimeOffset Published,
    DateTimeOffset Updated,
    string Url,
    string Summary,
    IEnumerable<string> Tags,
    string SearchQuery
) {
    public int? Id { get; set; }

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
        entry.Url.ToString(),
        aiResponse.Summary,
        aiResponse.Tags,
        aiResponse.SearchQuery
    )
    {
    }
}
