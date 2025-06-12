namespace GistBackend.Handlers.OpenAiHandler;

public record ChatClientHandlerOptions(
    string ApiKey,
    string Model = "gpt-4o-mini",
    string? ProjectId = null
);
