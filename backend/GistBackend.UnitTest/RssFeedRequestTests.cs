using System.Net;
using GistBackend.Handlers.RssFeedHandler.Feeds;

namespace GistBackend.UnitTest;

public class RssFeedRequestTests
{
    [Fact]
    public async Task KrebsFeed_UsesCanonicalHttpsUrl_AndKeepsExistingFeedIdentity()
    {
        using var handler = new FeedResponseHandler();
        using var client = new HttpClient(handler);
        var feed = new KrebsOnSecurity();

        await feed.ParseFeedAsync(client, CancellationToken.None);

        Assert.Equal(new Uri("https://krebsonsecurity.com/feed/"), handler.RequestUri);
        Assert.Equal(new Uri("https://krebsonsecurity.com/feed"), feed.ToRssFeedInfo().RssUrl);
        Assert.Equal("Krebs on Security", feed.Title);
    }

    [Fact]
    public async Task T3nFeed_SendsFeedReaderHeaders_AndKeepsExistingFeedIdentity()
    {
        using var handler = new FeedResponseHandler(requireFeedHeaders: true);
        using var client = new HttpClient(handler);
        var feed = new T3N();

        await feed.ParseFeedAsync(client, CancellationToken.None);

        Assert.Equal(new Uri("https://t3n.de/tag/security/rss.xml"), handler.RequestUri);
        Assert.Equal(feed.RssUrl, feed.ToRssFeedInfo().RssUrl);
        Assert.NotNull(feed.Title);
    }

    private sealed class FeedResponseHandler(bool requireFeedHeaders = false) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            RequestUri = request.RequestUri;
            if (requireFeedHeaders && (!request.Headers.UserAgent.Any() ||
                !request.Headers.Accept.Any(value => value.MediaType == "application/rss+xml")))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotAcceptable));
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    <rss version="2.0"><channel>
                      <title>Krebs on Security</title>
                      <link>https://krebsonsecurity.com/</link>
                      <description>Feed request regression test</description>
                    </channel></rss>
                    """),
            });
        }
    }
}
