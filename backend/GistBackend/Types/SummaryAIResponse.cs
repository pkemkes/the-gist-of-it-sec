namespace GistBackend.Types;

public record SummaryAIResponse(
    string Summary,
    IEnumerable<string> Tags,
    string SearchQuery
);
