using GistBackend.Exceptions;
using GistBackend.Types;
using HtmlAgilityPack;
using static System.Net.WebUtility;
using static GistBackend.Types.FeedType;
using static GistBackend.Types.Language;

namespace GistBackend.Handlers.RssFeedHandler.Feeds;

public record ArsTechnicaTechnologyLab() : RssFeed(
    new Uri("https://feeds.arstechnica.com/arstechnica/technology-lab"),
    ExtractText,
    En,
    News,
    ["Security"])
{
    private new static string ExtractText(string content)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(content);

        var entryContents = doc.DocumentNode.SelectNodes("//div[contains(@class, 'post-content')]");
        if (entryContents == null || entryContents.Count == 0)
        {
            throw new ExtractingEntryTextException("Missing container element");
        }

        var combinedText = string.Join("", entryContents.Select(node => node.InnerText));
        if (string.IsNullOrWhiteSpace(combinedText))
        {
            throw new ExtractingEntryTextException("No text found in container");
        }

        var decodedText = HtmlDecode(combinedText);
        return decodedText.Trim().Replace("\n", " ");
    }
}

