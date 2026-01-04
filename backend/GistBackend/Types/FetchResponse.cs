using System.Text.Json.Serialization;

namespace GistBackend.Types;

[method: JsonConstructor]
public record FetchResponse(int Status, string Content, bool Redirected);
