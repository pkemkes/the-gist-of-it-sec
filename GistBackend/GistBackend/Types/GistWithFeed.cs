using System.Text.Json.Serialization;

namespace GistBackend.Types;

[method: JsonConstructor]
public record GistWithFeed(
    int Id,
    string Reference,
    string FeedTitle,
    string FeedUrl,
    string Title,
    string Author,
    string Url,
    string Published,
    string Updated,
    string Summary,
    string[] Tags,
    string SearchQuery)
{
    public GistWithFeed(
        int Id,
        string Reference,
        string FeedTitle,
        string FeedUrl,
        string Title,
        string Author,
        string Url,
        string Published,
        string Updated,
        string Summary,
        string Tags,
        string SearchQuery)
        : this(
            Id,
            Reference,
            FeedTitle,
            FeedUrl,
            Title,
            Author,
            Url,
            Published,
            Updated,
            Summary,
            Tags.Split(";;", StringSplitOptions.RemoveEmptyEntries),
            SearchQuery
        )
    {
    }

    public static GistWithFeed FromGistAndFeed(Gist gist, RssFeedInfo feedInfo)
    {
        return new GistWithFeed(
            gist.Id!.Value,
            gist.Reference,
            feedInfo.Title,
            feedInfo.RssUrl.ToString(),
            gist.Title,
            gist.Author,
            gist.Url.ToString(),
            gist.Published.ToString("yyyy-MM-ddTHH:mm:ss.ffffffZ"), // 6 decimal places to match database
            gist.Updated.ToString("yyyy-MM-ddTHH:mm:ss.ffffffZ"), // 6 decimal places to match database
            gist.Summary,
            gist.Tags,
            gist.SearchQuery
        );
    }
};
