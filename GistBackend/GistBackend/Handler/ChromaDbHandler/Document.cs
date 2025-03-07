namespace GistBackend.Handler.ChromaDbHandler;

public record Document(
    string[] Ids,
    float[][] Embeddings,
    Metadata[] Metadatas
);
