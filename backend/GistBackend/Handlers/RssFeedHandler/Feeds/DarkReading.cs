using GistBackend.Exceptions;
using GistBackend.Types;
using HtmlAgilityPack;
using static System.Net.WebUtility;
using static GistBackend.Types.FeedType;
using static GistBackend.Types.Language;
using static GistBackend.Utils.RssFeedUtils;

namespace GistBackend.Handlers.RssFeedHandler.Feeds;

public record DarkReading : RssFeed
{
    public override Uri RssUrl => new("https://www.darkreading.com/rss.xml");
    public override Language Language => En;
    public override FeedType Type => News;

    public override string ExtractText(string content)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(content);

        var entryContentSingleNode =
            doc.DocumentNode.SelectSingleNode($"//div[{ContainsClassSpecifier("ArticleBase-BodyContent")}]");
        string textContent;

        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (entryContentSingleNode == null)
        {
            var entryContentMultiNode =
                doc.DocumentNode.SelectNodes(
                    $"//div[{ContainsClassSpecifier("ArticleMultiSectionBody-SectionContainer")}]");
            if (entryContentMultiNode == null || entryContentMultiNode.Count == 0)
            {
                throw new ExtractingEntryTextException("Missing single and multi node container elements");
            }

            textContent = string.Join("", entryContentMultiNode.Select(node => node.InnerText));
            if (string.IsNullOrWhiteSpace(textContent))
            {
                throw new ExtractingEntryTextException("No text found in multi node container");
            }
        }
        else
        {
            textContent = entryContentSingleNode.InnerText;
        }

        if (string.IsNullOrWhiteSpace(textContent))
        {
            throw new ExtractingEntryTextException("No text found in container");
        }

        var decodedText = HtmlDecode(textContent);
        return decodedText.Trim();
    }
}

