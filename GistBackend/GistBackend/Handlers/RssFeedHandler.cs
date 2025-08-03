using GistBackend.Types;

namespace GistBackend.Handlers;

public interface IRssFeedHandler
{
    List<RssFeed> Definitions { get; set; }
    Task ParseFeedAsync(RssFeed rssFeed, CancellationToken ct);
}

public class RssFeedHandler : IRssFeedHandler
{
    private readonly HttpClient _httpClient;
    public List<RssFeed> Definitions { get; set; }

    public RssFeedHandler(HttpClient httpClient)
    {
        _httpClient = httpClient;
        Definitions = [
            new RssFeed(
                new Uri("https://krebsonsecurity.com/feed"),
                content => content
            ),
            new RssFeed(
                new Uri("https://www.bleepingcomputer.com/feed/"),
                content => content,
                [ "Security" ]
            ),
            new RssFeed(
                new Uri("https://www.darkreading.com/rss.xml"),
                content => content
            ),
            new RssFeed(
                new Uri("https://www.theverge.com/rss/cyber-security/index.xml"),
                content => content
            ),
            new RssFeed(
                new Uri("https://feeds.feedblitz.com/GDataSecurityBlog-EN&x=1"),
                content => content
            ),
            new RssFeed(
                new Uri("https://therecord.media/feed"),
                content => content
            ),
            new RssFeed(
                new Uri("https://feeds.arstechnica.com/arstechnica/technology-lab"),
                content => content,
                [ "Security" ]
            )
        ];
    }

    public Task ParseFeedAsync(RssFeed rssFeed, CancellationToken ct) =>
        rssFeed.ParseFeedAsync(_httpClient, ct);
}
