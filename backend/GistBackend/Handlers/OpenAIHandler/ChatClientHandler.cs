using System.ClientModel;
using System.Text.Json;
using GistBackend.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using static GistBackend.Utils.LogEvents;

namespace GistBackend.Handlers.OpenAiHandler;

public interface IChatClientHandler
{
    public Task<string> CompleteChatAsync(List<ChatMessage> messages, ChatCompletionOptions options,
        CancellationToken ct);
}

public class ChatClientHandler : IChatClientHandler
{
    private readonly ILogger<ChatClientHandler>? _logger;
    private readonly ChatClient _client;

    public ChatClientHandler(IOptions<ChatClientHandlerOptions> options, ILogger<ChatClientHandler>? logger)
    {
        if (string.IsNullOrWhiteSpace(options.Value.ApiKey))
            throw new ArgumentException("API key is not set in the options.");
        _logger = logger;
        _client = options.Value.ProjectId is not null
            ? new ChatClient(options.Value.Model, new ApiKeyCredential(options.Value.ApiKey),
                new OpenAIClientOptions { ProjectId = options.Value.ProjectId })
            : new ChatClient(options.Value.Model, new ApiKeyCredential(options.Value.ApiKey));
    }

    public async Task<string> CompleteChatAsync(List<ChatMessage> messages, ChatCompletionOptions options,
        CancellationToken ct)
    {
        var runId = Guid.NewGuid();
        LogRunStart(runId, messages);
        var result = await _client.CompleteChatAsync(messages, options, ct);
        if (result is null) throw new ExternalServiceException("Unexpected error in call to API");
        LogRunResult(runId, result);
        if (result.Value.Content.Count != 1) throw new ExternalServiceException("Unexpected content length");
        return result.Value.Content.First().Text;
    }

    private void LogRunStart(Guid runId, List<ChatMessage> messages)
    {
        var serializedMessages = JsonSerializer.Serialize(messages.Select(JoinMessageToString));
        _logger?.LogDebug(GenerateChatCompletion, "Starting chat completion run {RunId} with messages: {Messages}",
            runId, serializedMessages);
    }

    private void LogRunResult(Guid runId, ClientResult<ChatCompletion> result)
    {
        var serializedResult = JsonSerializer.Serialize(result.Value.Content.Select(content => content.Text));
        _logger?.LogDebug(ChatCompletionGenerated, "Chat completion run {RunId} completed with result: {Result}",
            runId, serializedResult);
    }

    private static string JoinMessageToString(ChatMessage message) => string.Join("",
        message.Content.Select(contentPart => contentPart.Text));
}
