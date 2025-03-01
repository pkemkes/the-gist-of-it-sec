namespace GistBackend.Types;

public record RssFeedInfo(
    string Title,
    string RssUrl,
    string Language,
    int? Id = null
) {
    public int? Id { get; set; } = Id;
};
