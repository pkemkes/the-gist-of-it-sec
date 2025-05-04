namespace GistBackend.Handler.ChromaDbHandler;

public record ChromaDbHandlerOptions(
    string Server,
    string ServerAuthnCredentials,
    uint Port = 8000,
    string GistsTenantName = "the_gist_of_it_sec",
    string GistsDatabaseName = "the_gist_of_it_sec",
    string GistsCollectionName = "gist_text_contents",
    string CredentialsHeaderName = "X-Chroma-Token"
);
