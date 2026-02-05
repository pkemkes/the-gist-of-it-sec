using GistBackend.Exceptions;
using GistBackend.Types;
using HtmlAgilityPack;
using static System.Net.WebUtility;
using static GistBackend.Types.FeedType;
using static GistBackend.Types.Language;
using static GistBackend.Utils.RssFeedUtils;

namespace GistBackend.Handlers.RssFeedHandler.Feeds;

public record T3N() : RssFeed
{
    public override Uri RssUrl => new("https://t3n.de/tag/security/rss.xml");
    public override Language Language => De;
    public override FeedType Type => News;
    public override IEnumerable<string> ForbiddenCategories => ["jobs der woche"];

    public override bool CheckForPaywall(string content)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(content);

        var paywallDiv = doc.DocumentNode.SelectSingleNode($"//div[{ContainsClassSpecifier("c-paywall__wrapper")}]");
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        return paywallDiv is not null;
    }

    public override string ExtractText(string content)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(content);

        var entryContainer = doc.DocumentNode.SelectSingleNode("//div[@class='c-entry']");
        if (entryContainer == null)
        {
            throw new ExtractingEntryTextException("Missing container element");
        }

        var paragraphsAndHeadings =
            entryContainer.SelectNodes($".//p | .//div[{ContainsClassSpecifier("c-article__headline")}]");
        if (paragraphsAndHeadings == null || paragraphsAndHeadings.Count == 0)
        {
            throw new ExtractingEntryTextException("Missing paragraph or heading elements");
        }

        var combinedTextContents = string.Join("\n", paragraphsAndHeadings.Select(node => node.InnerText));
        if (string.IsNullOrWhiteSpace(combinedTextContents))
        {
            throw new ExtractingEntryTextException("No text found in container");
        }

        var decodedText = HtmlDecode(combinedTextContents);
        return decodedText.Trim();
    }
}
