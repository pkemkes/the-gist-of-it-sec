using System.Text.Json.Serialization;

namespace GistBackend.Types;

[method: JsonConstructor]
public record SimilarGist(
    Gist Gist,
    float Similarity
);
