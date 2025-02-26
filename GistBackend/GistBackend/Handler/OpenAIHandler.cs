using GistBackend.Types;

namespace GistBackend.Handler;

public interface IOpenAIHandler {
    public Task<AIResponse> ProcessEntryAsync(RssEntry entry, CancellationToken ct);
}

public class OpenAIHandler {
    public Task<AIResponse> ProcessEntryAsync(RssEntry entry, CancellationToken ct) =>
        Task.FromResult(new AIResponse(
            "fancy summary",
            [ "first tag", "second tag", "third tag" ],
            "fancy search query"
        ));
}
