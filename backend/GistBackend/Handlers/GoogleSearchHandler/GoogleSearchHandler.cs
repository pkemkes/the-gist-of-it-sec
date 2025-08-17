using System.Globalization;
using System.Net;
using GistBackend.Exceptions;
using GistBackend.Types;
using Google;
using Google.Apis.CustomSearchAPI.v1.Data;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using static GistBackend.Utils.LogEvents;

namespace GistBackend.Handlers.GoogleSearchHandler;

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

        return search?.Items?.Select(item => {
            try
            {
                var thumbnailUri = ExtractThumbnailUri(item);
                return new GoogleSearchResult(
                    gistId,
                    item.Title,
                    item.Snippet,
                    new Uri(item.Link),
                    item.DisplayLink,
                    ExtractThumbnailUri(item)
                );
            }
            catch (Exception ex) when (ex is UriFormatException or NullReferenceException)
            {
                logger?.LogError(ex, "Invalid URI in search result - Link: {Link}, DisplayLink: {DisplayLink}",
                    item.Link, item.DisplayLink);
                throw;
            }
        }).ToList();
    }

    private static Uri? ExtractThumbnailUri(Result item)
    {
        if (item.Pagemap is null) return null;
        var hasCseThumbnail = item.Pagemap.TryGetValue("cse_thumbnail", out var cseThumbnail);
        if (!hasCseThumbnail || cseThumbnail is not JArray { Count: > 0 } thumbnailList) return null;
        if (thumbnailList[0] is JObject thumbnail
            && thumbnail.TryGetValue("src", out var src)
            && src is JValue srcString)
        {
            return new Uri(srcString.ToString(CultureInfo.InvariantCulture));
        }

        return null;
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
                logger?.LogError(GoogleApiQuotaExceeded, ex, "Google API rate limit exceeded");
                return null;
            }

            const string message = "Unexpected Google API exception";
            logger?.LogError(UnexpectedGoogleApiException, ex, message);
            throw new ExternalServiceException(message, ex);
        }
    }
}
