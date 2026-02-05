namespace GistBackend.Utils;

public class RssFeedUtils
{
    /*
     * uses the “whole-word class” pattern: it normalizes whitespace, pads with spaces, then checks for ' className '.
     * This matches the class as a distinct token within a space-separated class list,
     * avoiding false positives like ' classNameExtra ' or ' extraClassName '.
    */
    public static string ContainsClassSpecifier(string className) =>
        $"contains(concat(' ', normalize-space(@class), ' '), ' {className} ')";
}
