using System.Net;
using System.ServiceModel.Syndication;
using System.Xml;
using GistBackend.Exceptions;
using GistBackend.Utils;

namespace GistBackend.Types;

public abstract record RssFeed()
{
    public abstract Uri RssUrl { get; }
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
        var response = await httpClient.GetAsync(RssUrl, ct);
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
