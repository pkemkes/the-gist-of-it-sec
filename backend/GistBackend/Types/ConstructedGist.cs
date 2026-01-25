using System.Text.Json.Serialization;
using GistBackend.Utils;

namespace GistBackend.Types;

[method: JsonConstructor]
public record ConstructedGist(
    int Id,
    string Reference,
    string FeedTitle,
    string FeedUrl,
    FeedType FeedType,
    string Title,
    string Author,
    bool IsSponsoredContent,
    string Url,
    string Published,
    string Updated,
    string Summary,
    string[] Tags)
{
    public ConstructedGist(
        int Id,
        string Reference,
        string FeedTitle,
        string FeedUrl,
        FeedType FeedType,
        string Title,
        string Author,
        bool IsSponsoredContent,
        string Url,
        string Published,
        string Updated,
        string Summary,
        string Tags)
        : this(
            Id,
            Reference,
            FeedTitle,
            FeedUrl,
            FeedType,
            Title,
            Author,
            IsSponsoredContent,
            Url,
            Published,
            Updated,
            Summary,
            Tags.Split(";;", StringSplitOptions.RemoveEmptyEntries)
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
            feedInfo.Type,
            summary.Title,
            gist.Author,
            gist.IsSponsoredContent,
            gist.Url.ToString(),
            gist.Published.ToDatabaseCompatibleString(),
            gist.Updated.ToDatabaseCompatibleString(),
            summary.SummaryText,
            gist.Tags
        );
    }
};
