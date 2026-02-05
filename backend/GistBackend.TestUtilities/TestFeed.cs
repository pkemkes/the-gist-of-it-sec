using GistBackend.Types;

namespace TestUtilities;

public record TestFeed(
    Uri RssUrl,
    Language Language,
    FeedType Type,
    IEnumerable<string>? AllowedCategories = null,
    IEnumerable<string>? ForbiddenCategories = null)
    : RssFeed
{
    public const string SponsoredContentMarker = "SPONSORED CONTENT";
    public const string PaywallContentMarker = "PAYWALL CONTENT";
    public override Uri RssUrl { get; } = RssUrl;
    public override Language Language { get; } = Language;
    public override FeedType Type { get; } = Type;
    public override IEnumerable<string>? AllowedCategories { get; } = AllowedCategories;
    public override IEnumerable<string>? ForbiddenCategories { get; } = ForbiddenCategories;
    public override string ExtractText(string content) => content;
    public override bool CheckForSponsoredContent(string content) => content.Contains(SponsoredContentMarker);
    public override bool CheckForPaywall(string content) => content.Contains(PaywallContentMarker);
};
