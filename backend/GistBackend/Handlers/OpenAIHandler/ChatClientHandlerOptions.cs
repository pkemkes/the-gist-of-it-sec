namespace GistBackend.Handlers.OpenAiHandler;

public record ChatClientHandlerOptions
{
    public string ApiKey { get; init; } = "";
    public string Model { get; init; } = "gpt-5-mini";
    public string? ProjectId { get; init; } = null;
}
