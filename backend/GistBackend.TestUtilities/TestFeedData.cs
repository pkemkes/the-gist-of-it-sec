using System.ServiceModel.Syndication;
using GistBackend.Types;
using static TestUtilities.TestData;

namespace TestUtilities;

public class TestFeedData
{
    public RssFeed RssFeed { get; } = CreateTestRssFeed(Language.De);
    public string SyndicationFeedXml => SyndicationFeed.ToEncodedXmlString();
    public RssFeedInfo RssFeedInfo =>
        new(SyndicationFeed.Title.Text, RssFeed.RssUrl, RssFeed.Language, RssFeed.Type, RssFeed.Id);
    public List<RssEntry> Entries { get; }
    public List<SummaryAIResponse> SummaryAIResponses { get; }
    public List<string> Texts { get; }
    public List<Gist> Gists { get; }
    private SyndicationFeed SyndicationFeed { get;  }

    public TestFeedData(List<RssEntry>? entries = null, List<SummaryAIResponse>? summaryAIResponses = null,
        List<string>? texts = null, List<Gist>? gists = null, int? feedId = null)
    {
        if (feedId is not null)
            RssFeed.Id = feedId.Value;
        Entries = entries ?? CreateTestEntries(5, feedId);
        SummaryAIResponses = summaryAIResponses ?? CreateTestSummaryAIResponses(Entries.Count);
        Texts = texts ?? CreateTestStrings(Entries.Count);
        Gists = gists ?? Entries.Zip(SummaryAIResponses, CreateTestGistFromEntry).ToList();
        SyndicationFeed = CreateTestSyndicationFeed(Entries);
    }
}
