namespace GistBackend.Handlers.ChromaDbHandler;

public record QueryResponse(
    string[][] Ids,
    Metadata[][] Metadatas,
    float[][] Distances
);
