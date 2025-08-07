using GistBackend.Handlers;
using GistBackend.Handlers.RssHandlers;

namespace GistBackend.UnitTest;

public class RssEntryHandlerTests
{
    [Fact]
    public async Task FetchTextContentAsync_ActualEntry_ReturnsCorrectText()
    {
        var rssFeedHandler = new RssFeedHandler(new HttpClient());
        var rssEntryHandler = new RssEntryHandler();
        foreach (var feed in rssFeedHandler.Definitions.Skip(rssFeedHandler.Definitions.Count-1))
        {
            await rssFeedHandler.ParseFeedAsync(feed, CancellationToken.None);
            feed.ParseEntries(0);
            var entry = feed.Entries?.FirstOrDefault();
            Assert.NotNull(entry);
            var reference = entry.Reference;
            Assert.NotEmpty(reference);
            var textContent = await rssEntryHandler.FetchTextContentAsync(entry, CancellationToken.None);
            Assert.NotEmpty(textContent);
        }
    }
}
