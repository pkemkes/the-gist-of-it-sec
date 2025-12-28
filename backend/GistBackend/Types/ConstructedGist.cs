using System.Text.Json.Serialization;
using GistBackend.Utils;

namespace GistBackend.Types;

[method: JsonConstructor]
public record ConstructedGist(
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
    public ConstructedGist(
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

    public static ConstructedGist FromGistFeedAndSummary(Gist gist, RssFeedInfo feedInfo, Summary summary)
    {
        return new ConstructedGist(
            gist.Id!.Value,
            gist.Reference,
            feedInfo.Title,
            feedInfo.RssUrl.ToString(),
            summary.Title,
            gist.Author,
            gist.Url.ToString(),
            gist.Published.ToDatabaseCompatibleString(),
            gist.Updated.ToDatabaseCompatibleString(),
            summary.SummaryText,
            gist.Tags,
            gist.SearchQuery
        );
    }
};
