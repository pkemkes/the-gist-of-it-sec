using GistBackend.Types;

namespace GistBackend.Definitions;

public static class DefinedFeeds {
    public static List<RssFeed> Definitions = [
        new("https://krebsonsecurity.com/feed") {
            ExtractText = content => content
        },
        new("https://www.bleepingcomputer.com/feed/") {
            ExtractText = content => content,
            AllowedCategories = [ "Security" ]
        },
        new("https://www.darkreading.com/rss.xml") {
            ExtractText = content => content
        },
        new("https://www.theverge.com/rss/cyber-security/index.xml") {
            ExtractText = content => content
        },
        new("https://feeds.feedblitz.com/GDataSecurityBlog-EN&x=1") {
            ExtractText = content => content
        },
        new("https://therecord.media/feed") {
            ExtractText = content => content
        },
        new("https://feeds.arstechnica.com/arstechnica/technology-lab") {
            ExtractText = content => content,
            AllowedCategories = [ "Security" ]
        }
    ];
}
