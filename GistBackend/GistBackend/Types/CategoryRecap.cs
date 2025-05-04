using System.Text.Json.Serialization;

namespace GistBackend.Types;

[method: JsonConstructor]
public record CategoryRecap(string Heading, string Recap, string Related)
{
    public CategoryRecap(string Heading, string Recap, IEnumerable<int> Related)
        : this(Heading, Recap, string.Join(";;", Related))
    {
    }
}
