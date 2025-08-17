using System.Text.Json.Serialization;

namespace GistBackend.Types;


[method: JsonConstructor]
public record Recap(IEnumerable<RecapSection> RecapSections);

[method: JsonConstructor]
public record RecapSection(string Heading, string Recap, IEnumerable<int> Related);

public record SerializedRecap(DateTime Created, string Recap, int? Id = null);

public record RelatedGistInfo(int Id, string Title);

public record DeserializedRecapSection(string Heading, string Recap, IEnumerable<RelatedGistInfo> Related);

public record DeserializedRecap(DateTime Created, IEnumerable<DeserializedRecapSection> RecapSections, int Id);
