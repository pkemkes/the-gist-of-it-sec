namespace GistBackend.Types;

public record AIResponse(
    string Summary,
    IEnumerable<string> Tags,
    string SearchQuery
);
