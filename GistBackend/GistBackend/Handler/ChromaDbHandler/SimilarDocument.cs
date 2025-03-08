namespace GistBackend.Handler.ChromaDbHandler;

public record SimilarDocument(
    string Reference,
    float Similarity
);
