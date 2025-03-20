using System.Net;
using GistBackend.Types;
using GistBackend.Utils;
using Google;
using Google.Apis.CustomSearchAPI.v1;
using Google.Apis.CustomSearchAPI.v1.Data;
using Google.Apis.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GistBackend.Handler.GoogleSearchHandler;

public record GoogleSearchHandlerOptions(
    string ApiKey,
    string EngineId
);

public interface IGoogleSearchHandler {
    public Task<List<GoogleSearchResult>?> GetSearchResultsAsync(string searchQuery, int gistId,
        CancellationToken ct);
}

public class GoogleSearchHandler(
    IOptions<GoogleSearchHandlerOptions> options,
    ILogger<GoogleSearchHandler>? logger,
    IHttpClientFactory httpClientFactory)
    : IGoogleSearchHandler
{
    private readonly CustomSearchAPIService _customSearchApiService = new(new BaseClientService.Initializer {
        ApiKey = options.Value.ApiKey,
        HttpClientFactory = new RetryingHttpClientFactory(httpClientFactory)
    });
    private readonly string _engineId = options.Value.EngineId;

    public async Task<List<GoogleSearchResult>?> GetSearchResultsAsync(string searchQuery, int gistId,
        CancellationToken ct)
    {
        using var loggingScope = logger?.BeginScope(
            "Fetching search results for gist {GistId} with query {SearchQuery}", gistId, searchQuery
        );
        var search = await ExecuteSearchAsync(searchQuery, ct);

        return search?.Items?.Select(item => new GoogleSearchResult(
            gistId,
            item.Title,
            item.Snippet,
            item.Link,
            item.DisplayLink,
            item.Image.ThumbnailLink
        )).ToList();
    }

    private async Task<Search?> ExecuteSearchAsync(string searchQuery, CancellationToken ct)
    {
        try
        {
            var listRequest = _customSearchApiService.Cse.List();
            listRequest.Cx = _engineId;
            listRequest.Q = searchQuery;
            return await listRequest.ExecuteAsync(ct);
        }
        catch (GoogleApiException ex)
        {
            if (ex.HttpStatusCode == HttpStatusCode.TooManyRequests)
            {
                logger?.LogError(LogEvents.GoogleApiQuotaExceeded, ex, "Google API rate limit exceeded");
            }
            else
            {
                logger?.LogError(LogEvents.UnexpectedGoogleApiException, ex, "Unexpected Google API exception");
            }

            return null;
        }
    }
}
