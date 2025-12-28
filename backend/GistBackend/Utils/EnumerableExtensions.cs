using GistBackend.Types;

namespace GistBackend.Utils;

public static class EnumerableExtensions {
    public static IEnumerable<RssEntry>
        FilterForAllowedCategories(this IEnumerable<RssEntry> collection, IEnumerable<string>? allowedCategories) =>
        allowedCategories is null
            ? collection
            : collection.Where(entry => entry.Categories.Any(allowedCategories.Contains));

    public static IEnumerable<RssEntry>
        FilterPaywallArticles(this IEnumerable<RssEntry> collection, string feedTitle) =>
        feedTitle.Contains("Golem")
            ? collection.Where(entry => !entry.Title.Contains("(g+)"))
            : collection;
}
