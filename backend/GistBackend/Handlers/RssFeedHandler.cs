using GistBackend.Types;
using HtmlAgilityPack;
using static System.Net.WebUtility;

namespace GistBackend.Handlers;

public interface IRssFeedHandler
{
    List<RssFeed> Definitions { get; set; }
    Task ParseFeedAsync(RssFeed rssFeed, CancellationToken ct);
}

public class RssFeedHandler(HttpClient httpClient) : IRssFeedHandler
{
    public List<RssFeed> Definitions { get; set; } = [
        new(
            new Uri("https://krebsonsecurity.com/feed"),
            ExtractTextKrebsOnSecurity
        ),
        new(
            new Uri("https://www.bleepingcomputer.com/feed/"),
            ExtractTextBleepingComputer,
            [ "Security" ]
        ),
        new(
            new Uri("https://www.darkreading.com/rss.xml"),
            ExtractTextDarkReading
        ),
        new(
            new Uri("https://www.theverge.com/rss/cyber-security/index.xml"),
            ExtractTextTheVerge
        ),
        new(
            new Uri("https://feeds.feedblitz.com/GDataSecurityBlog-EN&x=1"),
            ExtractTextGData
        ),
        new(
            new Uri("https://therecord.media/feed"),
            ExtractTextTheRecord
        ),
        new(
            new Uri("https://feeds.arstechnica.com/arstechnica/technology-lab"),
            ExtractTextArsTechnica,
            [ "Security" ]
        )
    ];

    public Task ParseFeedAsync(RssFeed rssFeed, CancellationToken ct) =>
        rssFeed.ParseFeedAsync(httpClient, ct);

    private static string ExtractTextKrebsOnSecurity(string content)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(content);

        var entryContent = doc.DocumentNode.SelectSingleNode("//div[@class='entry-content']");
        if (entryContent == null)
        {
            throw new InvalidOperationException("Missing container element");
        }

        var textContent = entryContent.InnerText;
        if (string.IsNullOrWhiteSpace(textContent))
        {
            throw new InvalidOperationException("No text found in container");
        }

        var decodedText = HtmlDecode(textContent);
        return decodedText.Trim();
    }

    private static string ExtractTextBleepingComputer(string content)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(content);

        var entryContent = doc.DocumentNode.SelectSingleNode("//div[@class='articleBody']");
        if (entryContent == null)
        {
            throw new InvalidOperationException("Missing container element");
        }

        var textContent = entryContent.InnerText;
        if (string.IsNullOrWhiteSpace(textContent))
        {
            throw new InvalidOperationException("No text found in container");
        }

        var decodedText = HtmlDecode(textContent);

        var trimmed = decodedText.Split("Related Articles:");
        return (trimmed.Length > 0 ? trimmed[0] : decodedText).Trim();
    }

    private static string ExtractTextDarkReading(string content)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(content);

        // Use contains() to match elements that have ArticleBase-BodyContent as one of their classes
        var entryContent = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'ArticleBase-BodyContent')]");
        if (entryContent == null)
        {
            throw new InvalidOperationException("Missing container element");
        }

        var textContent = entryContent.InnerText;
        if (string.IsNullOrWhiteSpace(textContent))
        {
            throw new InvalidOperationException("No text found in container");
        }

        var decodedText = HtmlDecode(textContent);
        return decodedText.Trim();
    }

    private static string ExtractTextTheVerge(string content)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(content);

        var entryContents = doc.DocumentNode.SelectNodes("//div[@class='duet--article--article-body-component']");
        if (entryContents == null || entryContents.Count == 0)
        {
            throw new InvalidOperationException("Missing container element");
        }

        var combinedText = string.Join("", entryContents.Select(node => node.InnerText));
        if (string.IsNullOrWhiteSpace(combinedText))
        {
            throw new InvalidOperationException("No text found in container");
        }

        var decodedText = HtmlDecode(combinedText);
        return decodedText.Trim().Replace("\n", " ");
    }

    private static string ExtractTextGData(string content)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(content);

        var entryContent = doc.DocumentNode.SelectSingleNode("//div[@class='nm-article-blog']");
        if (entryContent == null)
        {
            throw new InvalidOperationException("Missing container element");
        }

        var textContent = entryContent.InnerText;
        if (string.IsNullOrWhiteSpace(textContent))
        {
            throw new InvalidOperationException("No text found in container");
        }

        var decodedText = HtmlDecode(textContent);
        return decodedText.Trim();
    }

    private static string ExtractTextTheRecord(string content)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(content);

        var articleContent = doc.DocumentNode.SelectSingleNode("//div[@class='article__content']");
        if (articleContent == null)
        {
            throw new InvalidOperationException("Missing container element");
        }

        var entryContents = articleContent.SelectNodes(".//span[@class='wysiwyg-parsed-content']");
        if (entryContents == null || entryContents.Count == 0)
        {
            throw new InvalidOperationException("Missing container element");
        }

        var textContent = entryContents.First().InnerText;
        if (string.IsNullOrWhiteSpace(textContent))
        {
            throw new InvalidOperationException("No text found in container");
        }

        var decodedText = HtmlDecode(textContent);
        return decodedText.Trim();
    }

    private static string ExtractTextArsTechnica(string content)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(content);

        var entryContents = doc.DocumentNode.SelectNodes("//div[contains(@class, 'post-content')]");
        if (entryContents == null || entryContents.Count == 0)
        {
            throw new InvalidOperationException("Missing container element");
        }

        var combinedText = string.Join("", entryContents.Select(node => node.InnerText));
        if (string.IsNullOrWhiteSpace(combinedText))
        {
            throw new InvalidOperationException("No text found in container");
        }

        var decodedText = HtmlDecode(combinedText);
        return decodedText.Trim().Replace("\n", " ");
    }
}
