using System.ServiceModel.Syndication;
using System.Xml;
using GistBackend.Utils;

namespace GistBackend.Types;

public record RssFeed(string RssUrl) {
    public IEnumerable<string>? AllowedCategories { get; init; }
    public int? Id { get; set; }
    public string? Title { get; set; }
    public string? Language { get; set; }
    public Func<string, string>? ExtractText { get; init; }
    public IEnumerable<RssEntry> Entries { get; private set; } = [];

    private async Task<SyndicationFeed> LoadFeedAsync(CancellationToken ct)
    {
        using var httpClient = new HttpClient();
        var response = await httpClient.GetStringAsync(RssUrl, ct);
        using var stringReader = new StringReader(response);
        using var xmlReader = XmlReader.Create(stringReader);
        return SyndicationFeed.Load(xmlReader);
    }

    public async Task ParseFeedAsync(CancellationToken ct)
    {
        var feed = await LoadFeedAsync(ct);
        Title = feed.Title.Text;
        Language = feed.Language;
        Entries = feed.Items.Select(SyndicationItemToRssEntry).FilterForAllowedCategories(AllowedCategories);
    }

    private RssEntry SyndicationItemToRssEntry(SyndicationItem item)
    {
        if (Id is null) throw new Exception("Feed ID was not set");
        if (ExtractText is null) throw new Exception("ExtractText for feed was not set");
        return new RssEntry(
            item.Id.Trim(),
            Id.Value,
            item.ExtractAuthor(),
            item.Title.Text.Trim(),
            item.PublishDate,
            item.ExtractUpdated(),
            item.ExtractLink(),
            item.ExtractCategories(),
            ExtractText
        );
    }
}
