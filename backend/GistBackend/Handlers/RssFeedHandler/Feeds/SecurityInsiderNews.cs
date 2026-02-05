using GistBackend.Exceptions;
using GistBackend.Types;
using HtmlAgilityPack;
using static System.Net.WebUtility;
using static GistBackend.Types.FeedType;
using static GistBackend.Types.Language;
using static GistBackend.Utils.RssFeedUtils;

namespace GistBackend.Handlers.RssFeedHandler.Feeds;

public record SecurityInsiderNews : RssFeed
{
    public override Uri RssUrl => new("https://www.security-insider.de/rss/news.xml");
    public override Language Language => De;
    public override FeedType Type => News;

    public override string ExtractText(string content)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(content);

        var entryContainer = doc.DocumentNode.SelectSingleNode("//article[@class='inf-article-detail']");
        if (entryContainer == null)
        {
            throw new ExtractingEntryTextException("Missing container element");
        }

        var textContainerSelectors = new List<string>
        {
            ".//p[contains(@class, 'inf-text-')]",
            $".//h1[{ContainsClassSpecifier("inf-xheading")}]",
            $".//h2[{ContainsClassSpecifier("inf-xheading")}]",
            $".//h3[{ContainsClassSpecifier("inf-xheading")}]",
            $".//h4[{ContainsClassSpecifier("inf-xheading")}]",
            $".//h5[{ContainsClassSpecifier("inf-xheading")}]",
            $".//h6[{ContainsClassSpecifier("inf-xheading")}]"
        };
        var textContainers = entryContainer.SelectNodes(string.Join(" | ", textContainerSelectors));
        if (textContainers == null || textContainers.Count == 0)
        {
            throw new ExtractingEntryTextException("Missing text container elements");
        }

        var textContent = string.Join("\n", textContainers.Select(node => node.InnerText));
        if (string.IsNullOrWhiteSpace(textContent))
        {
            throw new ExtractingEntryTextException("No text found in containers");
        }

        var decodedText = HtmlDecode(textContent);
        return decodedText.Trim();
    }

    public override bool CheckForSponsoredContent(string content)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(content);
        var companiesSection = doc.DocumentNode.SelectSingleNode("//section[contains(@class, 'inf-companies-rel')]");
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        return companiesSection is not null;
    }
}

