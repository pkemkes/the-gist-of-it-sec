using GistBackend.Exceptions;
using GistBackend.Types;
using HtmlAgilityPack;
using static System.Net.WebUtility;
using static GistBackend.Types.FeedType;
using static GistBackend.Types.Language;

namespace GistBackend.Handlers.RssFeedHandler.Feeds;

public record TheRecord : RssFeed
{
    public override Uri RssUrl => new("https://therecord.media/feed");
    public override Language Language => En;
    public override FeedType Type => News;

    public override string ExtractText(string content)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(content);

        var articleContent = doc.DocumentNode.SelectSingleNode("//div[@class='article__content']");
        if (articleContent == null)
        {
            throw new ExtractingEntryTextException("Missing container element");
        }

        var entryContents = articleContent.SelectNodes(".//span[@class='wysiwyg-parsed-content']");
        if (entryContents == null || entryContents.Count == 0)
        {
            throw new ExtractingEntryTextException("Missing container element");
        }

        var textContent = entryContents.First().InnerText;
        if (string.IsNullOrWhiteSpace(textContent))
        {
            throw new ExtractingEntryTextException("No text found in container");
        }

        var decodedText = HtmlDecode(textContent);
        return decodedText.Trim();
    }
}

