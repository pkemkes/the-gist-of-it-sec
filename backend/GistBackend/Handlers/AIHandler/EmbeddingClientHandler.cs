using System.ClientModel;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Embeddings;

namespace GistBackend.Handlers.AIHandler;

public interface IEmbeddingClientHandler
{
    public Task<float[]> GenerateEmbeddingAsync(string input, CancellationToken ct);
    public string Model { get; }
}

public class EmbeddingClientHandler : IEmbeddingClientHandler
{
    private readonly EmbeddingClient _client;
    public string Model { get; }

    public EmbeddingClientHandler(IOptions<EmbeddingClientHandlerOptions> options)
    {
        if (string.IsNullOrWhiteSpace(options.Value.ApiKey))
            throw new ArgumentException("API key is not set in the options.");
        Model = options.Value.Model;
        _client = options.Value.ProjectId is not null
            ? new EmbeddingClient(Model, new ApiKeyCredential(options.Value.ApiKey),
                new OpenAIClientOptions { ProjectId = options.Value.ProjectId })
            : new EmbeddingClient(Model, new ApiKeyCredential(options.Value.ApiKey));
    }

    public async Task<float[]> GenerateEmbeddingAsync(string input, CancellationToken ct)
    {
        var result = await _client.GenerateEmbeddingAsync(input, cancellationToken: ct);
        return result.Value.ToFloats().ToArray();
    }
}

