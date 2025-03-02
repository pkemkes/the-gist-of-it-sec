namespace GistBackend.Types;

public record GoogleSearchResult(
    int GistId,
    string Title,
    string Snippet,
    string Url,
    string DisplayUrl,
    string ThumbnailUrl,
    string ImageUrl,
    int? Id = null
) {
    public int? Id { get; set; } = Id;
}
