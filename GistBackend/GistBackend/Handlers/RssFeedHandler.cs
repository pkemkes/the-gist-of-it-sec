using GistBackend.Types;

namespace GistBackend.Handlers;

public interface IRssFeedHandler
{
    List<RssFeed> Definitions { get; }
    Task ParseFeedAsync(RssFeed rssFeed, CancellationToken ct);
}

public class RssFeedHandler(HttpClient httpClient) : IRssFeedHandler
{
    public List<RssFeed> Definitions => [
        new(
            "https://krebsonsecurity.com/feed",
            content => content
        ),
        new(
            "https://www.bleepingcomputer.com/feed/",
            content => content,
            [ "Security" ]
        ),
        new(
            "https://www.darkreading.com/rss.xml",
            content => content
        ),
        new(
            "https://www.theverge.com/rss/cyber-security/index.xml",
            content => content
        ),
        new(
            "https://feeds.feedblitz.com/GDataSecurityBlog-EN&x=1",
            content => content
        ),
        new(
            "https://therecord.media/feed",
            content => content
        ),
        new(
            "https://feeds.arstechnica.com/arstechnica/technology-lab",
            content => content,
            [ "Security" ]
        )
    ];

    public Task ParseFeedAsync(RssFeed rssFeed, CancellationToken ct) => rssFeed.ParseFeedAsync(httpClient, ct);
}
