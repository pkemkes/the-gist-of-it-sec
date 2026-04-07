using Newtonsoft.Json;

namespace GistBackend.Types;

[method: JsonConstructor]
public record GistSearchResult(
    ConstructedGist Gist,
    float Similarity
);
