using GistBackend.Types;

namespace TestUtilities;

public record TestFeed(Uri RssUrl, Language Language, FeedType Type, IEnumerable<string>? AllowedCategories = null)
    : RssFeed(AllowedCategories)
{
    public const string SponsoredContentMarker = "SPONSORED CONTENT";
    public override Uri RssUrl { get; } = RssUrl;
    public override Language Language { get; } = Language;
    public override FeedType Type { get; } = Type;
    public override string ExtractText(string content) => content;
    public override bool CheckForSponsoredContent(string content) => content.Contains(SponsoredContentMarker);
};
