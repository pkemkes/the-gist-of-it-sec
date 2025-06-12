namespace GistBackend.Handlers.ChromaDbHandler;

public record Document(
    string[] Ids,
    Metadata[] Metadatas,
    float[][]? Embeddings = null
);
