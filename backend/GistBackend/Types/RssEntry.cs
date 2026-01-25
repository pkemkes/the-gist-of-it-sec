namespace GistBackend.Types;

public record RssEntry(
    string Reference,
    int FeedId,
    string Author,
    string Title,
    DateTime Published,
    DateTime Updated,
    Uri Url,
    IEnumerable<string> Categories
);
