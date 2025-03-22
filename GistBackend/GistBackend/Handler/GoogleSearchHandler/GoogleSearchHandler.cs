using System.Net;
using GistBackend.Exceptions;
using GistBackend.Types;
using GistBackend.Utils;
using Google;
using Google.Apis.CustomSearchAPI.v1.Data;
using Microsoft.Extensions.Logging;

namespace GistBackend.Handler.GoogleSearchHandler;

public interface IGoogleSearchHandler {
    public Task<List<GoogleSearchResult>?> GetSearchResultsAsync(string searchQuery, int gistId,
        CancellationToken ct);
}

public class GoogleSearchHandler(ICustomSearchApiHandler customSearchApiHandler, ILogger<GoogleSearchHandler>? logger)
    : IGoogleSearchHandler
{
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
            return await customSearchApiHandler.ExecuteSearchAsync(searchQuery, ct);
        }
        catch (GoogleApiException ex)
        {
            if (ex.HttpStatusCode == HttpStatusCode.TooManyRequests)
            {
                logger?.LogError(LogEvents.GoogleApiQuotaExceeded, ex, "Google API rate limit exceeded");
                return null;
            }

            const string message = "Unexpected Google API exception";
            logger?.LogError(LogEvents.UnexpectedGoogleApiException, ex, message);
            throw new ExternalServiceException(message, ex);
        }
    }
}
