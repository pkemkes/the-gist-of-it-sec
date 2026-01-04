namespace GistBackend.Handlers.AIHandler;

public record AIHandlerOptions
{
    public string Host { get; init; } = "http://aiapi:8000";
}
