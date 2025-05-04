namespace GistBackend.Types;

public record SerializedRecap(
    DateTime Created,
    string Recap,
    int? Id = null
)
{
    public int? Id { get; set; } = Id;
}
