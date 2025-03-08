namespace GistBackend.Handler.ChromaDbHandler;

public record Document(
    string[] Ids,
    Metadata[] Metadatas,
    float[][]? Embeddings = null
);
