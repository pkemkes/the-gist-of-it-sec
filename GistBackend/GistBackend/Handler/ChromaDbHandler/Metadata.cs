namespace GistBackend.Handler.ChromaDbHandler;

public record Metadata(
    string Reference,
    int FeedId,
    bool Disabled = false
);
