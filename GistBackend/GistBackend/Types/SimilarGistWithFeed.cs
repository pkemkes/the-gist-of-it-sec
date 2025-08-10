using System.Text.Json.Serialization;

namespace GistBackend.Types;

[method: JsonConstructor]
public record SimilarGist(
    GistWithFeed Gist,
    float Similarity
);
