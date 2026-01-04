using System.Text.Json;
using GistBackend.Exceptions;
using GistBackend.Types;
using GistBackend.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GistBackend.Handlers.WebCrawlHandler;

public interface IWebCrawlHandler
{
    Task<FetchResponse> FetchAsync(string url, CancellationToken ct);
}

public class WebCrawlHandler(
    HttpClient httpClient,
    IOptions<WebCrawlHandlerOptions> options,
    ILogger<WebCrawlHandler>? logger = null)
    : IWebCrawlHandler
{
    private readonly Uri _baseAddress = new(new Uri(options.Value.Host), "fetch");

    public async Task<FetchResponse> FetchAsync(string url, CancellationToken ct)
    {
        logger?.LogInformation("Fetching content");
        var parameters = new Dictionary<string, string> { { "url", url } };
        var query = await new FormUrlEncodedContent(parameters).ReadAsStringAsync(ct);
        var uriBuilder = new UriBuilder(_baseAddress) { Query = query };

        var response = await httpClient.GetAsync(uriBuilder.Uri, ct);
        response.EnsureSuccessStatusCode();

        var resultStream = await response.Content.ReadAsStreamAsync(ct);
        var result = await JsonSerializer.DeserializeAsync<FetchResponse>(resultStream, SerializerDefaults.JsonOptions,
            cancellationToken: ct);
        if (result is null) throw new ExternalServiceException("Failed to deserialize fetch response");
        logger?.LogInformation("Fetched content successfully. StatusCode: {Status}, Redirected: {Redirected}",
            result.Status, result.Redirected);

        return result;
    }
}
