using System.ServiceModel.Syndication;
using System.Xml;
using GistBackend.Utils;

namespace GistBackend.Types;

public record RssFeed(string RssUrl, Func<string, string> ExtractText, IEnumerable<string>? AllowedCategories = null) {
    public int? Id { get; set; }
    public string? Title { get; set; }
    public string? Language { get; set; }
    public IEnumerable<RssEntry> Entries { get; set; } = [];

    public async Task ParseFeedAsync(HttpClient httpClient, CancellationToken ct)
    {
        var response = await httpClient.GetStringAsync(RssUrl, ct);
        using var stringReader = new StringReader(response);
        using var xmlReader = XmlReader.Create(stringReader);
        var feed = SyndicationFeed.Load(xmlReader);
        Title = feed.Title.Text;
        Language = feed.Language;
        Entries = feed.Items.Select(SyndicationItemToRssEntry).FilterForAllowedCategories(AllowedCategories);
    }

    public RssFeedInfo ToRssFeedInfo()
    {
        if (Title is null) throw new ArgumentNullException($"{nameof(Title)} is null, need to parse feed first");
        if (Language is null) throw new ArgumentNullException($"{nameof(Language)} is null, need to parse feed first");
        return new RssFeedInfo(Title, RssUrl, Language) { Id = Id };
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
            item.PublishDate.UtcDateTime,
            item.ExtractUpdated(),
            item.ExtractUrl(),
            item.ExtractCategories(),
            ExtractText
        );
    }
}
