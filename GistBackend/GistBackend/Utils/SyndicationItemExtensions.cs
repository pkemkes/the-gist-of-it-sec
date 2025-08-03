using System.ServiceModel.Syndication;

namespace GistBackend.Utils;

public static class SyndicationItemExtensions {
    public static string ExtractAuthor(this SyndicationItem item)
    {
        if (item.Authors.Count != 0) return string.Join(", ", item.Authors.Select(person => person.Name.Trim()));
        return item.ElementExtensions
            .Where(ext => ext.OuterName == "creator")
            .Select(ext => ext.GetObject<string>())
            .FirstOrDefault()?.Trim() ?? "";
    }

    public static DateTime ExtractUpdated(this SyndicationItem item) =>
        item.LastUpdatedTime > DateTimeOffset.UnixEpoch
            ? item.LastUpdatedTime.UtcDateTime
            : item.PublishDate.UtcDateTime;

    public static Uri ExtractUrl(this SyndicationItem item) => item.Links.First().Uri;

    public static IEnumerable<string> ExtractCategories(this SyndicationItem item) =>
        item.Categories.Select(category => category.Name.Trim());
}
