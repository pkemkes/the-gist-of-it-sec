using System.ClientModel;
using GistBackend.Exceptions;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;

namespace GistBackend.Handler.OpenAiHandler;

public record ChatClientHandlerOptions(
    string ApiKey,
    string Model = "gpt-4o-mini",
    string? ProjectId = null
);

public interface IChatClientHandler
{
    public Task<string> CompleteChatAsync(IEnumerable<ChatMessage> messages, ChatCompletionOptions options,
        CancellationToken ct);
}

public class ChatClientHandler(IOptions<ChatClientHandlerOptions> options) : IChatClientHandler
{
    private readonly ChatClient _client = options.Value.ProjectId is not null
        ? new ChatClient(options.Value.Model, new ApiKeyCredential(options.Value.ApiKey),
            new OpenAIClientOptions { ProjectId = options.Value.ProjectId })
        : new ChatClient(options.Value.Model, new ApiKeyCredential(options.Value.ApiKey));

    public async Task<string> CompleteChatAsync(IEnumerable<ChatMessage> messages, ChatCompletionOptions options,
        CancellationToken ct)
    {
        var result = await _client.CompleteChatAsync(messages, options, ct);
        if (result is null) throw new ExternalServiceException("Unexpected error in call to API");
        if (result.Value.Content.Count != 1) throw new ExternalServiceException("Unexpected content length");
        return result.Value.Content.First().Text;
    }
}
