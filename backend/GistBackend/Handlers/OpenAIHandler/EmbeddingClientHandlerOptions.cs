namespace GistBackend.Handlers.OpenAiHandler;

public record EmbeddingClientHandlerOptions
{
    public string ApiKey { get; init; } = "";
    public string Model { get; init; } = "text-embedding-3-small";
    public string? ProjectId { get; init; } = null;
}
