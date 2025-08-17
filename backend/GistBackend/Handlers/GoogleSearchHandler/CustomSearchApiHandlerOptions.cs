namespace GistBackend.Handlers.GoogleSearchHandler;

public record CustomSearchApiHandlerOptions
{
    public string ApiKey { get; init; } = "";
    public string EngineId { get; init; } = "";
}
