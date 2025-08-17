namespace GistBackend.Handlers.OpenAiHandler;

public record ChatClientHandlerOptions
{
    public string ApiKey { get; init; } = "";
    public string Model { get; init; } = "gpt-4o-mini";
    public string? ProjectId { get; init; } = null;
}
