namespace GistBackend.Types;

public record SummaryAIResponse(
    string SummaryEnglish,
    string SummaryGerman,
    string TitleTranslated,
    IEnumerable<string> Tags
);
