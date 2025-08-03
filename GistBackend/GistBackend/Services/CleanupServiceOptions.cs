namespace GistBackend.Services;

public record CleanupServiceOptions
{
    public string[] DomainsToIgnore { get; init; } = ["feeds.feedblitz.com"];
};
