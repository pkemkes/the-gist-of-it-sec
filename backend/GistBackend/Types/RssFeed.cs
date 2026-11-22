using System.Net;
using System.ServiceModel.Syndication;
using System.Xml;
using GistBackend.Exceptions;
using GistBackend.Utils;

namespace GistBackend.Types;

public abstract record RssFeed()
{
    public abstract Uri RssUrl { get; }
    // Retrieval can use a canonical URL without changing the persisted feed identity.
    protected virtual Uri FetchUrl => RssUrl;
    public abstract Language Language { get; }
    public abstract FeedType Type { get; }
    public virtual IEnumerable<string>? AllowedCategories => null;
    public virtual IEnumerable<string>? ForbiddenCategories => null;
    private SyndicationFeed? SyndicationFeed { get; set; }
    public int? Id { get; set; }
    public string? Title { get; set; }
    public IEnumerable<RssEntry>? Entries { get; private set; }
    public abstract string ExtractText(string content);

    public virtual bool CheckForSponsoredContent(string content) => false;
    public virtual bool CheckForPaywall(string content) => false;

    public async Task ParseFeedAsync(HttpClient httpClient, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, FetchUrl);
        request.Headers.UserAgent.ParseAdd("TheGistOfITSec/1.0 (+https://github.com/pkemkes/the-gist-of-it-sec)");
        request.Headers.Accept.ParseAdd("application/rss+xml, application/atom+xml, application/xml, text/xml;q=0.9, */*;q=0.1");
        using var response = await httpClient.SendAsync(request, ct);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new ParsingFeedException(
                $"Failed to fetch RSS feed from {RssUrl}, status code: {response.StatusCode}");
        }
        var feedContent = await response.Content.ReadAsStringAsync(ct);
        using var stringReader = new StringReader(feedContent);
        using var xmlReader = XmlReader.Create(stringReader);
        SyndicationFeed = SyndicationFeed.Load(xmlReader);
        Title = SyndicationFeed.Title.Text;
    }

    public void ParseEntries(int feedId)
    {
        if (SyndicationFeed is null || Title is null)
            throw new InvalidOperationException($"{nameof(SyndicationFeed)} is null, need to parse feed first");
        Id = feedId;
        Entries = SyndicationFeed.Items.Select(SyndicationItemToRssEntry)
            .FilterForAllowedCategories(AllowedCategories)
            .FilterForForbiddenCategories(ForbiddenCategories)
            .FilterPaywallEntries(Title ?? "");
    }

    public RssFeedInfo ToRssFeedInfo()
    {
        return Title is null
            ? throw new ArgumentNullException($"{nameof(Title)} is null, need to parse feed first")
            : new RssFeedInfo(Title, RssUrl, Language, Type) { Id = Id };
    }

    private RssEntry SyndicationItemToRssEntry(SyndicationItem item) =>
        new(
            item.Id.Trim(),
            Id!.Value,
            item.ExtractAuthor(),
            WebUtility.HtmlDecode(item.Title.Text.Trim()),
            item.PublishDate.UtcDateTime,
            item.ExtractUpdated(),
            item.ExtractUrl(),
            item.ExtractCategories()
        );
}
