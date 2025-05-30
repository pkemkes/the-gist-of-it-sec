using System.Text.Json.Serialization;

namespace GistBackend.Types;

[method: JsonConstructor]
public record GoogleSearchResult(
    int GistId,
    string Title,
    string Snippet,
    string Url,
    string DisplayUrl,
    string ThumbnailUrl,
    int? Id = null
) {
    public int? Id { get; set; } = Id;
}
