namespace GistBackend.Handlers.TelegramBotClientHandler;

public record TelegramBotClientHandlerOptions
{
    public string BotToken { get; init; } = string.Empty;
}
