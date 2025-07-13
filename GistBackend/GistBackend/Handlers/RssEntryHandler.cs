using GistBackend.Types;
using Microsoft.Extensions.Logging;

namespace GistBackend.Handlers;

public interface IRssEntryHandler {
    public Task<string> FetchTextContentAsync(RssEntry entry, CancellationToken ct);
}

public class RssEntryHandler(IHttpClientFactory httpClientFactory, ILogger<RssEntryHandler>? logger) : IRssEntryHandler
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient(StartUp.RetryingHttpClientName);

    public async Task<string> FetchTextContentAsync(RssEntry entry, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, entry.Url);
        var response = await _httpClient.SendAsync(request, ct);
        var pageContent = await response.Content.ReadAsStringAsync(ct);
        return entry.ExtractText(pageContent);
    }
}
