namespace GistBackend.Types;

public record AIResponse(
    string Summary,
    string Tags,
    string SearchQuery
);
