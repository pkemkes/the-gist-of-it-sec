using GistBackend.Exceptions;
using GistBackend.Types;
using HtmlAgilityPack;
using static System.Net.WebUtility;
using static GistBackend.Types.FeedType;
using static GistBackend.Types.Language;

namespace GistBackend.Handlers.RssFeedHandler.Feeds;

public record HeiseSecurity : RssFeed
{
    public override Uri RssUrl => new("https://www.heise.de/security/feed.xml");
    public override Language Language => De;
    public override FeedType Type => News;

    public override string ExtractText(string content)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(content);

        var entryContainer = doc.DocumentNode.SelectSingleNode("//div[@class='article-content']");
        if (entryContainer == null)
        {
            throw new ExtractingEntryTextException("Missing container element");
        }

        var paragraphsAndHeadings = entryContainer.SelectNodes(".//p | .//h1 | .//h2 | .//h3 | .//h4 | .//h5 | .//h6");
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

