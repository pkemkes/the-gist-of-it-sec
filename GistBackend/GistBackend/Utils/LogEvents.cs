using Microsoft.Extensions.Logging;

namespace GistBackend.Utils;

public static class LogEvents {
    public static readonly EventId GistServiceDelayExceeded = new(100, nameof(GistServiceDelayExceeded));
    public static readonly EventId GistInserted = new(200, nameof(GistInserted));
    public static readonly EventId GistUpdated = new(201, nameof(GistUpdated));
    public static readonly EventId SearchResultsInserted = new(300, nameof(SearchResultsInserted));
    public static readonly EventId SearchResultsUpdated = new(301, nameof(SearchResultsUpdated));
    public static readonly EventId NoSearchResults = new(302, nameof(NoSearchResults));
    public static readonly EventId DocumentInserted = new(400, nameof(DocumentInserted));
    public static readonly EventId AIResponseJsonParsingError = new(500, nameof(AIResponseJsonParsingError));
    public static readonly EventId GoogleApiQuotaExceeded = new(600, nameof(GoogleApiQuotaExceeded));
    public static readonly EventId UnexpectedGoogleApiException = new(600, nameof(UnexpectedGoogleApiException));
}
