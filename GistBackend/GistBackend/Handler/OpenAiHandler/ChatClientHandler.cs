using System.ClientModel;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;

namespace GistBackend.Handler.OpenAiHandler;

public record ChatClientHandlerOptions(
    string ApiKey,
    string Model = "gpt-4o-mini",
    string? ProjectId = null
);

public class ChatClientHandler(IOptions<ChatClientHandlerOptions> options)
{
    public readonly ChatClient Client = options.Value.ProjectId is not null
        ? new ChatClient(options.Value.Model, new ApiKeyCredential(options.Value.ApiKey),
            new OpenAIClientOptions { ProjectId = options.Value.ProjectId })
        : new ChatClient(options.Value.Model, new ApiKeyCredential(options.Value.ApiKey));
}
