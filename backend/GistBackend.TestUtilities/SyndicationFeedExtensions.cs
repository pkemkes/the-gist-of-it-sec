using System.ServiceModel.Syndication;
using System.Text;
using System.Xml;

namespace TestUtilities;

public static class SyndicationFeedExtensions
{
    public static string ToEncodedXmlString(this SyndicationFeed feed)
    {
        var formatter = new Atom10FeedFormatter(feed);
        var output = new StringWriter();
        var settings = new XmlWriterSettings
        {
            Indent = true,
            Encoding = Encoding.UTF8
        };
        using var xmlWriter = XmlWriter.Create(output, settings);
        formatter.WriteTo(xmlWriter);
        xmlWriter.Flush();
        return output.ToString();
    }
}
