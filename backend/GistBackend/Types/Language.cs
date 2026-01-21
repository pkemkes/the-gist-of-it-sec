namespace GistBackend.Types;

public enum Language
{
    En,
    De
}

public static class LanguageExtensions
{
    public static Language Invert(this Language value) => value == Language.En ? Language.De : Language.En;
}
