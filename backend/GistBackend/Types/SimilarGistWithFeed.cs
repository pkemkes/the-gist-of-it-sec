using System.Text.Json.Serialization;

namespace GistBackend.Types;

[method: JsonConstructor]
public record SimilarGistWithFeed(
    GistWithFeed Gist,
    float Similarity
);
