using GistBackend.Types;

namespace GistBackend.Handler;

public interface IOpenAIHandler {
    public Task<float[]> GenerateEmbeddingsAsync(string text, CancellationToken ct);
    public Task<AIResponse> ProcessEntryAsync(RssEntry entry, CancellationToken ct);
}

public class OpenAIHandler(EmbeddingsClientHandler embeddingsClientHandler) : IOpenAIHandler {
    public async Task<float[]> GenerateEmbeddingsAsync(string text, CancellationToken ct)
    {
        var result = await embeddingsClientHandler.Client.GenerateEmbeddingsAsync([text], cancellationToken: ct);
        if (result.Value.Count != 1)
            throw new Exception($"Unexpected length of embedding arrays returned: {result.Value.Count}");
        return result.Value.Single().ToFloats().ToArray();
    }

    public Task<AIResponse> ProcessEntryAsync(RssEntry entry, CancellationToken ct) =>
        Task.FromResult(new AIResponse(
            "fancy summary",
            "first tag;;second tag;;third tag",
            "fancy search query"
        ));
}
