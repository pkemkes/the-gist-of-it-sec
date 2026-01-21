using GistBackend.Handlers.RssFeedHandler.Feeds;
using GistBackend.Types;

namespace GistBackend.Handlers.RssFeedHandler;

public interface IRssFeedHandler
{
    List<RssFeed> Definitions { get; set; }
    Task ParseFeedAsync(RssFeed rssFeed, CancellationToken ct);
}

public class RssFeedHandler(HttpClient httpClient) : IRssFeedHandler
{
    public List<RssFeed> Definitions { get; set; } = [
        new ArsTechnicaTechnologyLab(),
        new BleepingComputer(),
        new DarkReading(),
        new GDATASecurityBlog(),
        new GolemSecurity(),
        new HeiseSecurity(),
        new KrebsOnSecurity(),
        new SecurityInsiderNews(),
        new TheRecord(),
        new TheVerge()
    ];

    public Task ParseFeedAsync(RssFeed rssFeed, CancellationToken ct) =>
        rssFeed.ParseFeedAsync(httpClient, ct);
}
