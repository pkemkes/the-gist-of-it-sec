using GistBackend.Exceptions;
using GistBackend.Types;
using HtmlAgilityPack;
using static System.Net.WebUtility;
using static GistBackend.Types.FeedType;
using static GistBackend.Types.Language;

namespace GistBackend.Handlers.RssFeedHandler.Feeds;

public record BleepingComputer() : RssFeed(["Security"])
{
    public override Uri RssUrl => new("https://www.bleepingcomputer.com/feed/");
    public override Language Language => En;
    public override FeedType Type => News;

    public override string ExtractText(string content)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(content);

        var entryContent = doc.DocumentNode.SelectSingleNode("//div[@class='articleBody']");
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

        var trimmed = decodedText.Split("Related Articles:");
        return (trimmed.Length > 0 ? trimmed[0] : decodedText).Trim();
    }
}

