using System.ClientModel;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Embeddings;

namespace GistBackend.Handlers.OpenAiHandler;

public interface IEmbeddingClientHandler
{
    public Task<float[]> GenerateEmbeddingAsync(string input, CancellationToken ct);
}

public class EmbeddingClientHandler(IOptions<EmbeddingClientHandlerOptions> options) : IEmbeddingClientHandler
{
    private readonly EmbeddingClient _client = options.Value.ProjectId is not null
        ? new EmbeddingClient(options.Value.Model, new ApiKeyCredential(options.Value.ApiKey),
            new OpenAIClientOptions { ProjectId = options.Value.ProjectId })
        : new EmbeddingClient(options.Value.Model, new ApiKeyCredential(options.Value.ApiKey));

    public async Task<float[]> GenerateEmbeddingAsync(string input, CancellationToken ct)
    {
        var result = await _client.GenerateEmbeddingAsync(input, cancellationToken: ct);
        return result.Value.ToFloats().ToArray();
    }
}
