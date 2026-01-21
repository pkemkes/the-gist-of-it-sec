using GistBackend.Exceptions;
using GistBackend.Types;
using HtmlAgilityPack;
using static System.Net.WebUtility;
using static GistBackend.Types.FeedType;
using static GistBackend.Types.Language;

namespace GistBackend.Handlers.RssFeedHandler.Feeds;

public record SecurityInsiderNews() : RssFeed(
    new Uri("https://www.security-insider.de/rss/news.xml"),
    ExtractText,
    De,
    News)
{
    private new static string ExtractText(string content)
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
            ".//h1[contains(@class, 'inf-xheading')]",
            ".//h2[contains(@class, 'inf-xheading')]",
            ".//h3[contains(@class, 'inf-xheading')]",
            ".//h4[contains(@class, 'inf-xheading')]",
            ".//h5[contains(@class, 'inf-xheading')]",
            ".//h6[contains(@class, 'inf-xheading')]"
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
}

