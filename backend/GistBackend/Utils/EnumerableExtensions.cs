using GistBackend.Types;

namespace GistBackend.Utils;

public static class EnumerableExtensions {
    extension(IEnumerable<RssEntry> collection)
    {
        public IEnumerable<RssEntry>
            FilterForAllowedCategories(IEnumerable<string>? allowedCategories) =>
            allowedCategories is null
                ? collection
                : collection.Where(entry => entry.Categories.Any(allowedCategories.Contains));

        public IEnumerable<RssEntry>
            FilterForForbiddenCategories(IEnumerable<string>? forbiddenCategories) =>
            forbiddenCategories is null
                ? collection
                : collection.Where(entry => !entry.Categories.Any(forbiddenCategories.Contains));

        public IEnumerable<RssEntry>
            FilterPaywallEntries(string feedTitle) =>
            feedTitle.Contains("Golem")
                ? collection.Where(entry => !entry.Title.Contains("(g+)"))
                : collection;
    }
}
