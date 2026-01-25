using GistBackend.Exceptions;
using GistBackend.Types;
using HtmlAgilityPack;
using static System.Net.WebUtility;
using static GistBackend.Types.FeedType;
using static GistBackend.Types.Language;

namespace GistBackend.Handlers.RssFeedHandler.Feeds;

public record KrebsOnSecurity : RssFeed
{
    public override Uri RssUrl => new("https://krebsonsecurity.com/feed");
    public override Language Language => En;
    public override FeedType Type => Blog;

    public override string ExtractText(string content)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(content);

        var entryContent = doc.DocumentNode.SelectSingleNode("//div[@class='entry-content']");
        if (entryContent == null)
        {
            throw new ExtractingEntryTextException("Missing container element");
        }

        var textContent = entryContent.InnerText;
        if (string.IsNullOrWhiteSpace(textContent))
        {
            throw new ExtractingEntryTextException("No text found in container");
        }

        var decodedText = HtmlDecode(textContent);
        return decodedText.Trim();
    }
}
