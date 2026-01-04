using Microsoft.Extensions.Logging;

namespace GistBackend.Utils;

public static class LogEvents
{
    // ReSharper disable once FieldCanBeMadeReadOnly.Local
    private static int _eventId;

    public static readonly EventId GistServiceDelayExceeded = new(++_eventId, nameof(GistServiceDelayExceeded));
    public static readonly EventId DatabaseConnectionFailed = new(++_eventId, nameof(DatabaseConnectionFailed));

    public static readonly EventId GistInserted = new(++_eventId, nameof(GistInserted));
    public static readonly EventId GistUpdated = new(++_eventId, nameof(GistUpdated));
    public static readonly EventId OpeningTransactionFailed = new(++_eventId, nameof(OpeningTransactionFailed));
    public static readonly EventId CommittingTransactionFailed = new(++_eventId, nameof(CommittingTransactionFailed));
    public static readonly EventId GettingFeedInfoByUrlFailed = new(++_eventId, nameof(GettingFeedInfoByUrlFailed));
    public static readonly EventId InsertingFeedInfoFailed = new(++_eventId, nameof(InsertingFeedInfoFailed));
    public static readonly EventId UpdatingFeedInfoFailed = new(++_eventId, nameof(UpdatingFeedInfoFailed));
    public static readonly EventId GettingGistByReferenceFailed = new(++_eventId, nameof(GettingGistByReferenceFailed));
    public static readonly EventId InsertingGistFailed = new(++_eventId, nameof(InsertingGistFailed));
    public static readonly EventId InsertingSummaryFailed = new(++_eventId, nameof(InsertingSummaryFailed));
    public static readonly EventId UpdatingGistFailed = new(++_eventId, nameof(UpdatingGistFailed));
    public static readonly EventId UpdatingSummaryFailed = new(++_eventId, nameof(UpdatingSummaryFailed));
    public static readonly EventId DatabaseOperationRetry = new(++_eventId, nameof(DatabaseOperationRetry));
    public static readonly EventId FetchingPageContentFailed = new(++_eventId, nameof(FetchingPageContentFailed));
    public static readonly EventId ExtractingPageContentFailed = new(++_eventId, nameof(ExtractingPageContentFailed));
    public static readonly EventId EntryAlreadyExistsInChromaDb = new(++_eventId, nameof(EntryAlreadyExistsInChromaDb));
    public static readonly EventId ParsingFeedFailed = new(++_eventId, nameof(ParsingFeedFailed));

    public static readonly EventId DocumentInserted = new(++_eventId, nameof(DocumentInserted));

    public static readonly EventId NoGistsForDailyRecap = new(++_eventId, nameof(NoGistsForDailyRecap));
    public static readonly EventId DailyRecapCreated = new(++_eventId, nameof(DailyRecapCreated));
    public static readonly EventId NoGistsForWeeklyRecap = new(++_eventId, nameof(NoGistsForWeeklyRecap));
    public static readonly EventId WeeklyRecapCreated = new(++_eventId, nameof(WeeklyRecapCreated));
    public static readonly EventId CheckIfRecapExistsFailed = new(++_eventId, nameof(CheckIfRecapExistsFailed));
    public static readonly EventId GettingGistsForRecapFailed = new(++_eventId, nameof(GettingGistsForRecapFailed));
    public static readonly EventId CouldNotGetRecap = new(++_eventId, nameof(CouldNotGetRecap));

    public static readonly EventId ChangedDisabledStateOfGistInDb = new(++_eventId, nameof(ChangedDisabledStateOfGistInDb));
    public static readonly EventId ChangedMetadataOfGistInChromaDb = new(++_eventId, nameof(ChangedMetadataOfGistInChromaDb));
    public static readonly EventId GettingAllGistsFailed = new(++_eventId, nameof(GettingAllGistsFailed));
    public static readonly EventId EnsuringCorrectDisabledFailed = new(++_eventId, nameof(EnsuringCorrectDisabledFailed));
    public static readonly EventId GettingDisabledStateFailed = new(++_eventId, nameof(GettingDisabledStateFailed));

    public static readonly EventId GettingPreviousGistsWithFeedFailed = new(++_eventId, nameof(GettingPreviousGistsWithFeedFailed));
    public static readonly EventId GettingAllFeedInfosFailed = new(++_eventId, nameof(GettingAllFeedInfosFailed));
    public static readonly EventId NoRecapFound = new(++_eventId, nameof(NoRecapFound));
    public static readonly EventId GettingLatestRecapFailed = new(++_eventId, nameof(GettingLatestRecapFailed));

    public static readonly EventId ErrorInHttpRequest = new(++_eventId, nameof(ErrorInHttpRequest));

    public static readonly EventId ChatRegisterCheckFailed = new(++_eventId, nameof(ChatRegisterCheckFailed));
    public static readonly EventId NoRecentGistFound = new(++_eventId, nameof(NoRecentGistFound));
    public static readonly EventId ChatRegistered = new(++_eventId, nameof(ChatRegistered));
    public static readonly EventId ChatDeregistered = new(++_eventId, nameof(ChatDeregistered));
    public static readonly EventId RegisteringChatFailed = new(++_eventId, nameof(RegisteringChatFailed));
    public static readonly EventId DeregisteringChatFailed = new(++_eventId, nameof(DeregisteringChatFailed));
    public static readonly EventId SentTelegramMessage = new(++_eventId, nameof(SentTelegramMessage));
    public static readonly EventId UnexpectedTelegramError = new(++_eventId, nameof(UnexpectedTelegramError));
    public static readonly EventId GettingAllChatsFailed = new(++_eventId, nameof(GettingAllChatsFailed));
    public static readonly EventId GettingNextFiveGistsWithFeedFailed = new(++_eventId, nameof(GettingNextFiveGistsWithFeedFailed));
    public static readonly EventId SendingGistToChatFailed = new(++_eventId, nameof(SendingGistToChatFailed));
    public static readonly EventId SettingGistIdLastSentFailed = new(++_eventId, nameof(SettingGistIdLastSentFailed));

    public static readonly EventId TelegramCommandNotRecognized = new(++_eventId, nameof(TelegramCommandNotRecognized));
    public static readonly EventId StartCommandButAlreadyRegistered = new(++_eventId, nameof(StartCommandButAlreadyRegistered));
    public static readonly EventId StartCommandForNewChat = new(++_eventId, nameof(StartCommandForNewChat));
    public static readonly EventId StopCommandButNotRegistered = new(++_eventId, nameof(StopCommandButNotRegistered));
    public static readonly EventId StopCommandForExistingChat = new(++_eventId, nameof(StopCommandForExistingChat));
    public static readonly EventId SendingGistToChat = new(++_eventId, nameof(SendingGistToChat));

    public static readonly EventId DidNotFindExpectedFeedInDb = new(++_eventId, nameof(DidNotFindExpectedFeedInDb));
}
