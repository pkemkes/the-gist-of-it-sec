using GistBackend.Exceptions;
using GistBackend.Types;
using HtmlAgilityPack;
using static System.Net.WebUtility;
using static GistBackend.Types.Language;

namespace GistBackend.Handlers;

public interface IRssFeedHandler
{
    List<RssFeed> Definitions { get; set; }
    Task ParseFeedAsync(RssFeed rssFeed, CancellationToken ct);
}

public partial class RssFeedHandler(HttpClient httpClient) : IRssFeedHandler
{
    public List<RssFeed> Definitions { get; set; } = [
        new(new Uri("https://krebsonsecurity.com/feed"), ExtractTextKrebsOnSecurity, En),
        new(new Uri("https://www.bleepingcomputer.com/feed/"), ExtractTextBleepingComputer, En, ["Security"]),
        new(new Uri("https://www.darkreading.com/rss.xml"), ExtractTextDarkReading, En),
        new(new Uri("https://www.theverge.com/rss/cyber-security/index.xml"), ExtractTextTheVerge, En),
        new(new Uri("https://feeds.feedblitz.com/GDataSecurityBlog-EN&x=1"), ExtractTextGData, En),
        new(new Uri("https://therecord.media/feed"), ExtractTextTheRecord, En),
        new(new Uri("https://feeds.arstechnica.com/arstechnica/technology-lab"), ExtractTextArsTechnica, En, ["Security"]),
        new(new Uri("https://www.heise.de/security/feed.xml"), ExtractTextHeise, De),
        new(new Uri("https://www.security-insider.de/rss/news.xml"), ExtractTextSecurityInsider, De),
        // new(new Uri("https://rss.golem.de/rss.php?ms=security&feed=ATOM1.0"), ExtractTextGolem)  // Golem has ad accept popup that blocks content
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

    private static string ExtractTextBleepingComputer(string content)
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

    private static string ExtractTextDarkReading(string content)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(content);

        // Use contains() to match elements that have ArticleBase-BodyContent as one of their classes
        var entryContentSingleNode = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'ArticleBase-BodyContent')]");
        string textContent;

        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (entryContentSingleNode == null)
        {
            var entryContentMultiNode =
                doc.DocumentNode.SelectNodes("//div[contains(@class, 'ArticleMultiSectionBody-SectionContainer')]");
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

    private static string ExtractTextTheVerge(string content)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(content);

        var entryContents = doc.DocumentNode.SelectNodes("//div[@class='duet--article--article-body-component']");
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

    private static string ExtractTextGData(string content)
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

    private static string ExtractTextTheRecord(string content)
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

    private static string ExtractTextArsTechnica(string content)
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

    private static string ExtractTextHeise(string content)
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

    private static string ExtractTextSecurityInsider(string content)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(content);

        var entryContainer = doc.DocumentNode.SelectSingleNode("//article[@class='inf-article-detail']");
        if (entryContainer == null)
        {
            throw new ExtractingEntryTextException("Missing container element");
        }

        var textContainerSelectors = new List<string> {
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

    // private static string ExtractTextGolem(string content)
    // {
    //     var doc = new HtmlDocument();
    //     doc.LoadHtml(content);
    //
    //     var entryContainer = doc.DocumentNode.SelectSingleNode("//article[contains(@class, 'go-article')]");
    //     if (entryContainer == null)
    //     {
    //         throw new ExtractingEntryTextException("Missing container element");
    //     }
    //
    //     var paragraphsAndHeadings = entryContainer.SelectNodes(".//p | .//h1 | .//h2 | .//h3 | .//h4 | .//h5 | .//h6");
    //     if (paragraphsAndHeadings == null || paragraphsAndHeadings.Count == 0)
    //     {
    //         throw new ExtractingEntryTextException("Missing paragraph or heading elements");
    //     }
    //
    //     var combinedTextContents = string.Join("\n", paragraphsAndHeadings.Select(node => node.InnerText));
    //     if (string.IsNullOrWhiteSpace(combinedTextContents))
    //     {
    //         throw new ExtractingEntryTextException("No text found in container");
    //     }
    //
    //     var decodedText = HtmlDecode(combinedTextContents);
    //     return decodedText;
    // }
}
