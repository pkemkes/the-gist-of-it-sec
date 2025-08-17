using System.Text.Json.Serialization;

namespace GistBackend.Types;

[method: JsonConstructor]
public record GoogleSearchResult(
    int GistId,
    string Title,
    string Snippet,
    Uri Url,
    string DisplayUrl,
    Uri? ThumbnailUrl,
    int? Id = null
) {
    public int? Id { get; set; } = Id;
}
