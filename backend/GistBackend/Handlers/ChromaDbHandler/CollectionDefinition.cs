using System.Text.Json.Serialization;

namespace GistBackend.Handlers.ChromaDbHandler;

[method: JsonConstructor]
public record CollectionDefinition(string Name)
{
    public MetadataConfiguration Metadata = new();
}

public record MetadataConfiguration
{
    [JsonPropertyName("hnsw:space")]
    public string Space = "cosine";
}
