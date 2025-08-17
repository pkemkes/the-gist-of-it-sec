using GistBackend.Types;

namespace GistBackend.Utils;

public static class EnumerableExtensions {
    public static IEnumerable<RssEntry> FilterForAllowedCategories(this IEnumerable<RssEntry> collection,
        IEnumerable<string>? allowedCategories)
    {
        return allowedCategories is null
            ? collection
            : collection.Where(entry => entry.Categories.Any(allowedCategories.Contains));
    }
}
