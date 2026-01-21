using GistBackend.Exceptions;
using GistBackend.Types;
using HtmlAgilityPack;
using static System.Net.WebUtility;
using static GistBackend.Types.FeedType;
using static GistBackend.Types.Language;

namespace GistBackend.Handlers.RssFeedHandler.Feeds;

public record GDATASecurityBlog() : RssFeed(
    new Uri("https://feeds.feedblitz.com/GDataSecurityBlog-EN&x=1"),
    ExtractText,
    En,
    Blog)
{
    private new static string ExtractText(string content)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(content);

        var entryContent = doc.DocumentNode.SelectSingleNode("//div[@class='nm-article-blog']");
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

