namespace GistBackend.Types;

public record RssEntry(
    string Reference,
    int FeedId,
    string Author,
    string Title,
    DateTimeOffset Published,
    DateTimeOffset Updated,
    Uri Url,
    IEnumerable<string> Categories,
    Func<string, string> ExtractText
) {
    public string DummyUserAgent = "Mozilla/5.0 (X11; Ubuntu; Linux x86_64; rv:131.0) Gecko/20100101 Firefox/131.0";
}
