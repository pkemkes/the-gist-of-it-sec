using System.Text.Json.Serialization;

namespace GistBackend.Types;

[method: JsonConstructor]
public record RssFeedInfo(
    string Title,
    Uri RssUrl,
    Language Language,
    FeedType Type,
    int? Id = null
) {
    public int? Id { get; set; } = Id;
};
