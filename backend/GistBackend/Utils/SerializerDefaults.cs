using System.Text.Json;

namespace GistBackend.Utils;

public static class SerializerDefaults
{
    public static readonly JsonSerializerOptions JsonOptions = new() {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };
}
