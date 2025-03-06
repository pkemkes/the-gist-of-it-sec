using System.ClientModel;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Embeddings;

namespace GistBackend.Handler;

public record EmbeddingsClientHandlerOptions(
    string ApiKey,
    string Model = "text-embedding-3-small",
    string? ProjectId = null
);

public abstract class EmbeddingsClientHandler(IOptions<EmbeddingsClientHandlerOptions> options) {
    public readonly EmbeddingClient Client = options.Value.ProjectId is not null
        ? new EmbeddingClient(options.Value.Model, new ApiKeyCredential(options.Value.ApiKey),
            new OpenAIClientOptions { ProjectId = options.Value.ProjectId })
        : new EmbeddingClient(options.Value.Model, new ApiKeyCredential(options.Value.ApiKey));
}
