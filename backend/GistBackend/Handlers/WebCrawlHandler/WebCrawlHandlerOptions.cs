namespace GistBackend.Handlers.WebCrawlHandler;

public record WebCrawlHandlerOptions
{
    public string Host { get; init; } = "http://fetcher:8000";
}
