using Microsoft.Extensions.Logging;

namespace GistBackend.Utils;

public static class LogEvents {
    public static readonly EventId GistServiceDelayExceeded = new(100, nameof(GistServiceDelayExceeded));
    public static readonly EventId DatabaseConnectionFailed = new(101, nameof(DatabaseConnectionFailed));
    public static readonly EventId GistInserted = new(200, nameof(GistInserted));
    public static readonly EventId GistUpdated = new(201, nameof(GistUpdated));
    public static readonly EventId GettingFeedInfoByUrlFailed = new(202, nameof(GettingFeedInfoByUrlFailed));
    public static readonly EventId InsertingFeedInfoFailed = new(203, nameof(InsertingFeedInfoFailed));
    public static readonly EventId UpdatingFeedInfoFailed = new(204, nameof(UpdatingFeedInfoFailed));
    public static readonly EventId GettingGistByReferenceFailed = new(205, nameof(GettingGistByReferenceFailed));
    public static readonly EventId InsertingGistFailed = new(206, nameof(InsertingGistFailed));
    public static readonly EventId UpdatingGistFailed = new(207, nameof(UpdatingGistFailed));
    public static readonly EventId DatabaseOperationRetry = new(208, nameof(DatabaseOperationRetry));
    public static readonly EventId SearchResultsInserted = new(300, nameof(SearchResultsInserted));
    public static readonly EventId SearchResultsUpdated = new(301, nameof(SearchResultsUpdated));
    public static readonly EventId NoSearchResults = new(302, nameof(NoSearchResults));
    public static readonly EventId GettingSearchResultsFailed = new(303, nameof(GettingSearchResultsFailed));
    public static readonly EventId DeletingSearchResultsFailed = new(304, nameof(DeletingSearchResultsFailed));
    public static readonly EventId DocumentInserted = new(400, nameof(DocumentInserted));
    public static readonly EventId SummaryAIResponseJsonParsingError = new(500, nameof(SummaryAIResponseJsonParsingError));
    public static readonly EventId RecapAIResponseJsonParsingError = new(501, nameof(RecapAIResponseJsonParsingError));
    public static readonly EventId GoogleApiQuotaExceeded = new(600, nameof(GoogleApiQuotaExceeded));
    public static readonly EventId UnexpectedGoogleApiException = new(601, nameof(UnexpectedGoogleApiException));
    public static readonly EventId NoGistsForDailyRecap = new(700, nameof(NoGistsForDailyRecap));
    public static readonly EventId DailyRecapCreated = new(701, nameof(DailyRecapCreated));
    public static readonly EventId NoGistsForWeeklyRecap = new(702, nameof(NoGistsForWeeklyRecap));
    public static readonly EventId WeeklyRecapCreated = new(703, nameof(WeeklyRecapCreated));
    public static readonly EventId CheckIfRecapExistsFailed = new(704, nameof(CheckIfRecapExistsFailed));
    public static readonly EventId GettingGistsForRecapFailed = new(705, nameof(GettingGistsForRecapFailed));
    public static readonly EventId ChangedDisabledStateOfGistInDb = new(800, nameof(ChangedDisabledStateOfGistInDb));
    public static readonly EventId ChangedMetadataOfGistInChromaDb = new(801, nameof(ChangedMetadataOfGistInChromaDb));
    public static readonly EventId GettingAllGistsFailed = new(802, nameof(GettingAllGistsFailed));
    public static readonly EventId EnsuringCorrectDisabledFailed = new(803, nameof(EnsuringCorrectDisabledFailed));
    public static readonly EventId GettingDisabledStateFailed = new(804, nameof(GettingDisabledStateFailed));
    public static readonly EventId GettingPreviousGistsFailed = new(900, nameof(GettingPreviousGistsFailed));
    public static readonly EventId ErrorInHttpRequest = new(1000, nameof(ErrorInHttpRequest));
}
