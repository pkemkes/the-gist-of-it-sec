using System.ServiceModel.Syndication;
using GistBackend.Types;
using static TestUtilities.TestData;

namespace TestUtilities;

public class TestFeedData
{
    public RssFeed RssFeed { get; } = CreateTestRssFeed();
    public string SyndicationFeedXml => SyndicationFeed.ToEncodedXmlString();
    public RssFeedInfo RssFeedInfo =>
        new(SyndicationFeed.Title.Text, RssFeed.RssUrl, SyndicationFeed.Language, RssFeed.Id);
    public List<RssEntry> Entries { get; }
    public List<Gist> Gists { get; }
    private SyndicationFeed SyndicationFeed { get;  }

    public TestFeedData(List<RssEntry>? entries = null, int? feedId = null)
    {
        if (feedId is not null)
            RssFeed.Id = feedId.Value;
        Entries = entries ?? CreateTestEntries(5, feedId);
        Gists = Entries.Select(CreateTestGistFromEntry).ToList();
        SyndicationFeed = CreateTestSyndicationFeed(Entries);
    }
}
