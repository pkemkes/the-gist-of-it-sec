using System.Text.Json.Serialization;

namespace GistBackend.Types;


[method: JsonConstructor]
public record RecapAIResponse(
    IEnumerable<RecapSection> RecapSectionsEnglish,
    IEnumerable<RecapSection> RecapSectionsGerman
);

[method: JsonConstructor]
public record RecapSection(string Heading, string Recap, IEnumerable<int> Related);

public record SerializedRecap(DateTime Created, string RecapEn, string RecapDe, int? Id = null);

public record RelatedGistInfo(int Id, string Title);

public record DeserializedRecapSection(string Heading, string Recap, IEnumerable<RelatedGistInfo> Related);

public record DeserializedRecap(DateTime Created, IEnumerable<DeserializedRecapSection> RecapSections, int Id);
