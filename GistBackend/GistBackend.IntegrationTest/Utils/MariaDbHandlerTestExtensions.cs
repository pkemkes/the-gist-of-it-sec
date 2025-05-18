using GistBackend.Handler.MariaDbHandler;
using GistBackend.Types;
using static GistBackend.IntegrationTest.Utils.TestData;

namespace GistBackend.IntegrationTest.Utils;

public static class MariaDbHandlerTestExtensions
{
    public static async Task<List<Gist>> InsertTestGistsAsync(this IMariaDbHandler handler, int count)
    {
        var testFeedId = await handler.InsertFeedInfoAsync(CreateTestFeedInfo(), CancellationToken.None);
        var gists = Enumerable.Range(0, count).Select(_ => CreateTestGist(testFeedId)).ToList();
        foreach (var gist in gists) gist.Id = await handler.InsertGistAsync(gist, CancellationToken.None);
        gists.Reverse();  // Expecting gists to be queried in descending order by Id
        return gists;
    }
}
