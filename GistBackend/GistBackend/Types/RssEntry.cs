namespace GistBackend.Types;

public record RssEntry(
    string Reference,
    int FeedId,
    string Author,
    string Title,
    DateTime Published,
    DateTime Updated,
    string Url,
    IEnumerable<string> Categories,
    Func<string, string> ExtractText
);
