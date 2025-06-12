namespace GistBackend.Handlers.OpenAiHandler;

public record EmbeddingClientHandlerOptions(
    string ApiKey,
    string Model = "text-embedding-3-small",
    string? ProjectId = null
);
