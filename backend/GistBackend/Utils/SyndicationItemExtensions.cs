using System.ServiceModel.Syndication;

namespace GistBackend.Utils;

public static class SyndicationItemExtensions {
    extension(SyndicationItem item)
    {
        public string ExtractAuthor()
        {
            if (item.Authors is null) return "";
            if (item.Authors.Count != 0) return string.Join(", ", item.Authors.Select(person => person.ExtractName()));
            return item.ElementExtensions
                .Where(ext => ext.OuterName == "creator")
                .Select(ext => ext.GetObject<string>())
                .FirstOrDefault()?.Trim() ?? "";
        }

        public DateTime ExtractUpdated() =>
            item.LastUpdatedTime > DateTimeOffset.UnixEpoch
                ? item.LastUpdatedTime.UtcDateTime
                : item.PublishDate.UtcDateTime;

        public Uri ExtractUrl() => item.Links.First(link => link.RelationshipType != "enclosure").Uri;

        public IEnumerable<string> ExtractCategories() =>
            item.Categories.Select(category => category.Name.Trim());
    }
}

public static class SyndicationPersonExtensions {
    public static string ExtractName(this SyndicationPerson person) =>
        string.IsNullOrWhiteSpace(person.Name)
            ? string.IsNullOrWhiteSpace(person.Email)
                ? ""
                : person.Email.Trim().Replace("(", "").Replace(")", "")
            : person.Name.Trim();
}
