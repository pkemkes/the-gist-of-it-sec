using System.Text.Json.Serialization;

namespace GistBackend.Types;

[method: JsonConstructor]
public class Recap(
    DateTime Created,
    IEnumerable<CategoryRecap> Recap
);
