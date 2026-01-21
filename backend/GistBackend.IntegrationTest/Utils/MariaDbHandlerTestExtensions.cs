using GistBackend.Handlers.MariaDbHandler;
using GistBackend.Types;
using static TestUtilities.TestData;

namespace GistBackend.IntegrationTest.Utils;

public static class MariaDbHandlerTestExtensions
{
    private static readonly Random Random = new();

    extension(IMariaDbHandler handler)
    {
        public async Task<List<RssFeedInfo>> InsertTestFeedInfosAsync(Language language, int count)
        {
            var feedInfos = Enumerable.Range(0, count).Select(_ => CreateTestFeedInfo(language)).ToList();
            foreach (var feedInfo in feedInfos)
            {
                feedInfo.Id = await handler.InsertFeedInfoAsync(feedInfo, CancellationToken.None);
            }
            return feedInfos;
        }

        public async Task<Gist> InsertTestGistAsync(int? feedId = null) =>
            (await handler.InsertTestGistsAsync(1, feedId)).Single();

        public async Task<List<Gist>> InsertTestGistsAsync(int count,
            int? feedId = null)
        {
            feedId ??= (await handler.InsertTestFeedInfosAsync(Language.De, 1)).Single().Id!.Value;
            var gists = Enumerable.Range(0, count).Select(_ => CreateTestGist(feedId.Value)).ToList();
            foreach (var gist in gists) gist.Id = await handler.InsertGistAsync(gist, CancellationToken.None);
            gists.Reverse();  // Expecting gists to be queried in descending order by Id
            return gists;
        }

        public async Task<List<ConstructedGist>> InsertTestConstructedGistsAsync(int count, RssFeedInfo? feed = null, LanguageMode languageMode = LanguageMode.Original)
        {
            feed ??= (await handler.InsertTestFeedInfosAsync(Language.De, 1)).Single();
            var constructedGists = new List<ConstructedGist>();
            for (var i = 0; i < count; i++)
            {
                var gist = CreateTestGist(feed.Id);
                gist.Id = await handler.InsertGistAsync(gist, CancellationToken.None);
                var summaries = await handler.InsertTestSummariesAsync(gist.Id!.Value, feed.Language);
                var summary = languageMode == LanguageMode.Original ? summaries.First()
                    : languageMode == LanguageMode.De && feed.Language == Language.De ? summaries.First()
                    : summaries.Last();
                constructedGists.Add(ConstructedGist.FromGistFeedAndSummary(gist, feed, summary));
            }
            constructedGists.Reverse(); // Expecting gists to be queried in descending order by Id
            return constructedGists;
        }

        public async Task<List<Summary>> InsertTestSummariesAsync(int gistId,
            Language feedLanguage)
        {
            var summaryOrig = CreateTestSummary(feedLanguage, false, gistId);
            var summaryTranslated = CreateTestSummary(feedLanguage.Invert(), true, gistId);

            await handler.InsertSummaryAsync(summaryOrig, CancellationToken.None);
            await handler.InsertSummaryAsync(summaryTranslated, CancellationToken.None);

            return [summaryOrig, summaryTranslated];
        }

        public async Task<List<Chat>> InsertTestChatsAsync(int count)
        {
            var gistWithFeedLastSent =
                (await handler.GetPreviousConstructedGistsAsync(1, null, [], null, [], LanguageMode.Original,
                    CancellationToken.None)).FirstOrDefault();
            var chats = Enumerable.Range(0, count)
                .Select(_ => new Chat(Random.NextInt64(), gistWithFeedLastSent?.Id - 5 ?? 0)).ToList();
            foreach (var chat in chats) await handler.RegisterChatAsync(chat.Id, CancellationToken.None);
            return chats;
        }
    }
}
