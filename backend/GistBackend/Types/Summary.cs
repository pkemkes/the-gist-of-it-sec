using System.Text.Json.Serialization;

namespace GistBackend.Types;

[method: JsonConstructor]
public record Summary(
    int GistId,
    Language Language,
    bool IsTranslated,
    string Title,
    string SummaryText,
    int? Id = null
){
    public int? Id { get; set; } = Id;
}
