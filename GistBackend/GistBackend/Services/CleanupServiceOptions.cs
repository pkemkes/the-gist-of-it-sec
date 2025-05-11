namespace GistBackend.Services;

public record CleanupServiceOptions
{
    public string[] DomainsToIgnore { get; init; } = ["https://feeds.feedblitz.com"];
};
