namespace GistBackend.Handlers.ChromaDbHandler;

public record Metadata(
    string Reference,
    int FeedId,
    bool Disabled = false
);
