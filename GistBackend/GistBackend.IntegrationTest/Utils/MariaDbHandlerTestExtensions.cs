using GistBackend.Handler.MariaDbHandler;
using GistBackend.Types;
using static GistBackend.IntegrationTest.Utils.TestData;

namespace GistBackend.IntegrationTest.Utils;

public static class MariaDbHandlerTestExtensions
{
    public static async Task<List<RssFeedInfo>> InsertTestFeedInfosAsync(this IMariaDbHandler handler, int count)
    {
        var feedInfos = Enumerable.Range(0, count).Select(_ => CreateTestFeedInfo()).ToList();
        foreach (var feedInfo in feedInfos)
        {
            feedInfo.Id = await handler.InsertFeedInfoAsync(feedInfo, CancellationToken.None);
        }
        return feedInfos;
    }

    public static async Task<List<Gist>> InsertTestGistsAsync(this IMariaDbHandler handler, int count,
        int? feedId = null)
    {
        feedId ??= (await handler.InsertTestFeedInfosAsync(1)).Single().Id!.Value;
        var gists = Enumerable.Range(0, count).Select(_ => CreateTestGist(feedId.Value)).ToList();
        foreach (var gist in gists) gist.Id = await handler.InsertGistAsync(gist, CancellationToken.None);
        gists.Reverse();  // Expecting gists to be queried in descending order by Id
        return gists;
    }

    public static async Task<List<GoogleSearchResult>> InsertTestSearchResultsAsync(this IMariaDbHandler handler,
        int count, int gistId)
    {
        var searchResults = Enumerable.Range(0, count).Select(_ => CreateTestSearchResult(gistId)).ToList();
        await handler.InsertSearchResultsAsync(searchResults, CancellationToken.None);
        return await handler.GetSearchResultsByGistIdAsync(gistId, CancellationToken.None);
    }
}
