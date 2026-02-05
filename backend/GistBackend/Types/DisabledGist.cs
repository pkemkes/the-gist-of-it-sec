namespace GistBackend.Types;

public record DisabledGist : Gist
{
    public DisabledGist(RssEntry entry) : base(
        entry.Reference,
        entry.FeedId,
        entry.Author,
        false,
        entry.Published,
        entry.Updated,
        entry.Url,
        "")
    {
    }
}
