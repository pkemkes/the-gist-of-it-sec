using System.Text.Json.Serialization;

namespace GistBackend.Types;

[method: JsonConstructor]
public record Chat(
    long Id,
    int GistIdLastSent
);
