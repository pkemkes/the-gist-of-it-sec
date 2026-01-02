using System.Net.Http.Json;
using GistBackend.Exceptions;
using GistBackend.Types;
using GistBackend.Utils;
using Microsoft.Extensions.Options;
using SharpToken;

namespace GistBackend.Handlers.AIHandler;

public record SummarizeRequest(string Title, string Article, string Language);

public record SummaryForRecap(string Title, string Summary, int Id);

public record RecapRequest(List<SummaryForRecap> Summaries, string RecapType);

public interface IAIHandler
{
    public Task<float[]> GenerateEmbeddingAsync(string input, CancellationToken ct);
    public Task<SummaryAIResponse> GenerateSummaryAIResponseAsync(Language feedLanguage, string title, string article,
        CancellationToken ct);
    public Task<RecapAIResponse> GenerateDailyRecapAsync(IEnumerable<ConstructedGist> gists, CancellationToken ct);
    public Task<RecapAIResponse> GenerateWeeklyRecapAsync(IEnumerable<ConstructedGist> gists, CancellationToken ct);
}

public class AIHandler : IAIHandler
{
    private readonly IEmbeddingClientHandler _embeddingClientHandler;
    private readonly HttpClient _httpClient;
    private readonly GptEncoding _encoding;

    public AIHandler(IEmbeddingClientHandler embeddingClientHandler, HttpClient httpClient,
        IOptions<AIHandlerOptions> options)
    {
        _embeddingClientHandler = embeddingClientHandler;
        _encoding = GptEncoding.GetEncodingForModel(embeddingClientHandler.Model);
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(options.Value.Host);
    }

    public Task<float[]> GenerateEmbeddingAsync(string input, CancellationToken ct)
    {
        // Tokenize and truncate to stay under 8k context; keep a small buffer for prompts.
        const int maxTokens = 7500;
        var tokens = _encoding.Encode(input);
        var safeInput = tokens.Count > maxTokens ? _encoding.Decode(tokens.Take(maxTokens)) : input;

        return _embeddingClientHandler.GenerateEmbeddingAsync(safeInput, ct);
    }

    public async Task<SummaryAIResponse> GenerateSummaryAIResponseAsync(Language feedLanguage, string title,
        string article, CancellationToken ct)
    {
        var request = new SummarizeRequest(title, article, feedLanguage.ToString());
        var response = await _httpClient.PostAsJsonAsync("/summarize", request, SerializerDefaults.JsonOptions, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new ExternalServiceException($"Failed to get summary from AI API: {response.StatusCode}");
        }

        var aiResponse =
            await response.Content.ReadFromJsonAsync<SummaryAIResponse>(cancellationToken: ct,
                options: SerializerDefaults.JsonOptions);
        return aiResponse ?? throw new ExternalServiceException("Could not parse summary AI response");
    }

    public Task<RecapAIResponse> GenerateDailyRecapAsync(IEnumerable<ConstructedGist> gists, CancellationToken ct) =>
        GenerateRecapAsync(gists, RecapType.Daily, ct);

    public Task<RecapAIResponse> GenerateWeeklyRecapAsync(IEnumerable<ConstructedGist> gists, CancellationToken ct) =>
        GenerateRecapAsync(gists, RecapType.Weekly, ct);

    private async Task<RecapAIResponse> GenerateRecapAsync(IEnumerable<ConstructedGist> gists, RecapType recapType,
        CancellationToken ct)
    {
        var summaries = gists.Select(gist => new SummaryForRecap(gist.Title, gist.Summary, gist.Id)).ToList();
        var request = new RecapRequest(summaries, recapType.ToString());
        var response = await _httpClient.PostAsJsonAsync("/recap", request, SerializerDefaults.JsonOptions, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new ExternalServiceException($"Failed to get recap from AI API: {response.StatusCode}");
        }
        var aiResponse =
            await response.Content.ReadFromJsonAsync<RecapAIResponse>(cancellationToken: ct,
                options: SerializerDefaults.JsonOptions);
        return aiResponse ?? throw new ExternalServiceException("Could not parse recap AI response");
    }
}
