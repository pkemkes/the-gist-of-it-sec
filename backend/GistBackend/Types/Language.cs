namespace GistBackend.Types;

public enum Language
{
    En,
    De
}

public static class LanguageExtensions
{
    public static Language Invert(this Language value) => value == Language.En ? Language.De : Language.En;
    public static string ToFullName(this Language value) => value switch
    {
        Language.En => "English",
        Language.De => "German",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };
}
