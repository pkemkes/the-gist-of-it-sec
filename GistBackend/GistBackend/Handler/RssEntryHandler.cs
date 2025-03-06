using GistBackend.Types;
using Microsoft.Extensions.Logging;

namespace GistBackend.Handler;

public interface IRssEntryHandler {
    public Task<string> FetchTextContentAsync(RssEntry entry, CancellationToken ct);
}

public class RssEntryHandler(HttpClient httpClient, ILogger<RssEntryHandler>? logger) : IRssEntryHandler {
    private const string DummyUserAgent =
        "Mozilla/5.0 (X11; Ubuntu; Linux x86_64; rv:131.0) Gecko/20100101 Firefox/131.0";

    public async Task<string> FetchTextContentAsync(RssEntry entry, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, entry.Url);
        request.Headers.Add("User-Agent", DummyUserAgent);
        var response = await httpClient.SendAsync(request, ct);
        var pageContent = await response.Content.ReadAsStringAsync(ct);
        return entry.ExtractText(pageContent);
    }
}
