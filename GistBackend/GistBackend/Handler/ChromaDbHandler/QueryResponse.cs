namespace GistBackend.Handler.ChromaDbHandler;

public record QueryResponse(
    string[][] Ids,
    Metadata[][] Metadatas,
    float[][] Distances
);
