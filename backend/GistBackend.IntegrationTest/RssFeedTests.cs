using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using GistBackend.Types;

namespace GistBackend.IntegrationTest;

public class RssFeedTests : IAsyncLifetime {
    private readonly IContainer _container = new ContainerBuilder("nginx:latest")
        .WithPortBinding(80, true)
        .WithResourceMapping(new DirectoryInfo("testData"), "/usr/share/nginx/html/")
        .WithWaitStrategy(Wait.ForUnixContainer()
            .UntilHttpRequestIsSucceeded(r => r.ForPort(80).ForPath("/test_atom.xml")))
        .Build();

    private string GetBaseUrl() => $"http://{_container.Hostname}:{_container.GetMappedPublicPort(80)}/";

    public async Task InitializeAsync() => await _container.StartAsync();
    public async Task DisposeAsync() => await _container.StopAsync();

    [Fact]
    public async Task ParseFeedAsync_Rss2FeedXML_FeedIsCorrectlyParsed()
    {
        var rssFeedUrl = new Uri($"{GetBaseUrl()}/test_rss_2.xml");
        var rssFeed = new RssFeed(rssFeedUrl, content => content, Language.En);

        await rssFeed.ParseFeedAsync(new HttpClient(), CancellationToken.None);
        rssFeed.ParseEntries(0);

        Assert.Equal("Test RSS Feed", rssFeed.Title);
        Assert.Equal(Language.En, rssFeed.Language);
        Assert.Equal(3, rssFeed.Entries!.Count());
        var entries = rssFeed.Entries!.ToArray();
        Assert.Equal("First news article", entries[0].Title);
        Assert.Equal("Second news article", entries[1].Title);
        Assert.Equal("Last news article", entries[2].Title);
        Assert.Equal(new Uri("https://www.test-news-site.com/first-news-article"), entries[0].Url);
        Assert.Equal(new Uri("https://www.test-news-site.com/second-news-article"), entries[1].Url);
        Assert.Equal(new Uri("https://www.test-news-site.com/last-news-article"), entries[2].Url);
        Assert.Equal(DateTimeOffset.Parse("Mon, 24 Feb 2025 15:00:00 GMT"), entries[0].Published);
        Assert.Equal(DateTimeOffset.Parse("Tue, 25 Feb 2025 16:00:00 GMT"), entries[1].Published);
        Assert.Equal(DateTimeOffset.Parse("Fri, 28 Feb 2025 17:00:00 GMT"), entries[2].Published);
        Assert.Equal(DateTimeOffset.Parse("Mon, 24 Feb 2025 15:00:00 GMT"), entries[0].Updated);
        Assert.Equal(DateTimeOffset.Parse("Tue, 25 Feb 2025 16:00:00 GMT"), entries[1].Updated);
        Assert.Equal(DateTimeOffset.Parse("Fri, 28 Feb 2025 17:00:00 GMT"), entries[2].Updated);
        Assert.Equal("Test Author 1", entries[0].Author);
        Assert.Equal("Test Author 2", entries[1].Author);
        Assert.Equal("Test Author 3", entries[2].Author);
        Assert.Equal("https://www.test-news-site.com/first-news-article", entries[0].Reference);
        Assert.Equal("https://www.test-news-site.com/second-news-article", entries[1].Reference);
        Assert.Equal("https://www.test-news-site.com/last-news-article", entries[2].Reference);
        Assert.Equivalent(new List<string>{ "Test Category 1", "Test Category 2" }, entries[0].Categories.ToList());
        Assert.Equivalent(new List<string>{ "Test Category 1" }, entries[1].Categories.ToList());
        Assert.Equivalent(new List<string>{ "Test Category 2" }, entries[2].Categories.ToList());
    }

    [Fact]
    public async Task ParseFeedAsync_AtomFeedXML_FeedIsCorrectlyParsed()
    {
        var rssFeedUrl = new Uri($"{GetBaseUrl()}/test_atom.xml");
        var rssFeed = new RssFeed(rssFeedUrl, content => content, Language.En);

        await rssFeed.ParseFeedAsync(new HttpClient(), CancellationToken.None);
        rssFeed.ParseEntries(0);

        Assert.Equal("Test RSS Feed", rssFeed.Title);
        Assert.Equal(Language.En, rssFeed.Language);
        Assert.Equal(3, rssFeed.Entries!.Count());
        var entries = rssFeed.Entries!.ToArray();
        Assert.Equal("First news article", entries[0].Title);
        Assert.Equal("Second news article", entries[1].Title);
        Assert.Equal("Last news article", entries[2].Title);
        Assert.Equal(new Uri("https://www.test-news-site.com/first-news-article"), entries[0].Url);
        Assert.Equal(new Uri("https://www.test-news-site.com/second-news-article"), entries[1].Url);
        Assert.Equal(new Uri("https://www.test-news-site.com/last-news-article"), entries[2].Url);
        Assert.Equal(DateTimeOffset.Parse("2025-02-25T16:38:43-05:00"), entries[0].Published);
        Assert.Equal(DateTimeOffset.Parse("2025-02-24T12:11:07-05:00"), entries[1].Published);
        Assert.Equal(DateTimeOffset.Parse("2025-02-24T06:37:02-05:00"), entries[2].Published);
        Assert.Equal(DateTimeOffset.Parse("2025-02-25T16:55:32-05:00"), entries[0].Updated);
        Assert.Equal(DateTimeOffset.Parse("2025-02-24T12:30:57-05:00"), entries[1].Updated);
        Assert.Equal(DateTimeOffset.Parse("2025-02-24T06:37:06-05:00"), entries[2].Updated);
        Assert.Equal("Test Author 1", entries[0].Author);
        Assert.Equal("Test Author 2", entries[1].Author);
        Assert.Equal("Test Author 3, Additional Author", entries[2].Author);
        Assert.Equal("https://www.test-news-site.com/first-news-article", entries[0].Reference);
        Assert.Equal("https://www.test-news-site.com/second-news-article", entries[1].Reference);
        Assert.Equal("https://www.test-news-site.com/last-news-article", entries[2].Reference);
        Assert.Equivalent(new List<string>{ "Test Category 1", "Test Category 2" }, entries[0].Categories.ToList());
        Assert.Equivalent(new List<string>{ "Test Category 1" }, entries[1].Categories.ToList());
        Assert.Equivalent(new List<string>{ "Test Category 2" }, entries[2].Categories.ToList());
    }

    [Theory]
    [InlineData("test_rss_2.xml")]
    [InlineData("test_atom.xml")]
    public async Task ParseFeedAsync_FeedContainsEntriesWithoutAllowedCategory_OnlyEntriesWithAllowedCategoriesParsed(
        string rssFeedPath)
    {
        var rssFeedUrl = new Uri($"{GetBaseUrl()}/{rssFeedPath}");
        var rssFeed = new RssFeed(rssFeedUrl, content => content, Language.De, ["Test Category 1"]);

        await rssFeed.ParseFeedAsync(new HttpClient(), CancellationToken.None);
        rssFeed.ParseEntries(0);

        Assert.Equal(2, rssFeed.Entries!.Count());
        Assert.DoesNotContain("https://www.test-news-site.com/last-news-article",
            rssFeed.Entries!.Select(entry => entry.Reference));
    }
}
