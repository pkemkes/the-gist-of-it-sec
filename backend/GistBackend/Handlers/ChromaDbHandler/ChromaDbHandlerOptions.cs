namespace GistBackend.Handlers.ChromaDbHandler;

public record ChromaDbHandlerOptions
{
    public string Server { get; init; } = "";
    public string ServerAuthnCredentials { get; init; } = "";
    public uint Port { get; init; } = 8000;
    public string GistsTenantName { get; init; } = "the_gist_of_it_sec";
    public string GistsDatabaseName { get; init; } = "the_gist_of_it_sec";
    public string GistsCollectionName { get; init; } = "gist_text_contents";
    public string CredentialsHeaderName { get; init; } = "X-Chroma-Token";
}
