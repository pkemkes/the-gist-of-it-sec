using System.Collections;
using System.Globalization;
using System.Text.Json;
using GistBackend.Types;
using OpenAI.Chat;

namespace GistBackend.Handler.OpenAiHandler;

public interface IOpenAIHandler {
    public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct);
    public Task<AIResponse> GenerateSummaryTagsAndQueryAsync(RssEntry entry, CancellationToken ct);
}

public class OpenAIHandler : IOpenAIHandler
{
    private readonly EmbeddingClientHandler _embeddingClientHandler;
    private readonly ChatClientHandler _chatClientHandler;
    private readonly Lazy<Task<IEnumerable<string>>> _tags;

    public OpenAIHandler(EmbeddingClientHandler embeddingClientHandler, ChatClientHandler chatClientHandler)
    {
        _embeddingClientHandler = embeddingClientHandler;
        _chatClientHandler = chatClientHandler;
        _tags = new Lazy<Task<IEnumerable<string>>>(LoadTagsAsync);
    }

    private async Task<IEnumerable<string>> LoadTagsAsync()
    {
        var tags = await JsonSerializer.DeserializeAsync<string[]>(File.OpenRead("tags.json"));
        if (tags is null) throw new Exception("Could not load and parse tags");
        return tags;
    }

    private Task<IEnumerable<string>> GetTagsAsync() => _tags.Value;

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct)
    {
        var result = await _embeddingClientHandler.Client.GenerateEmbeddingsAsync([text], cancellationToken: ct);
        if (result.Value.Count != 1)
            throw new Exception($"Unexpected length of embedding arrays returned: {result.Value.Count}");
        return result.Value.Single().ToFloats().ToArray();
    }

    public async Task<AIResponse> GenerateSummaryTagsAndQueryAsync(RssEntry entry, string text, CancellationToken ct)
    {
        var messages = await CreateChatMessagesAsync(entry.Title, text);
        var result = await _chatClientHandler.Client.CompleteChatAsync(messages new ChatCompletionOptions {
            ResponseFormat = 
        })
        return new AIResponse(
            "fancy summary",
            "first tag;;second tag;;third tag",
            "fancy search query"
        );
    }

    private async Task<IEnumerable<ChatMessage>> CreateChatMessagesAsync(string title, string text)
    {
        return [
            await CreateSystemMessageAsync(await GetTagsAsync()),
            await CreateUserMessageAsync(title, text)
        ];
    }

    private async Task<SystemChatMessage> CreateSystemMessageAsync(IEnumerable<string> tags)
    {
        var messageTemplate = await LoadTextFromFileAsync("SystemMessage.txt");
        var nowDateString = DateTimeOffset.UtcNow.ToString("R", CultureInfo.InvariantCulture);
        var messageContent = messageTemplate
            .Replace("{now}", nowDateString)
            .Replace("{tags}", string.Join(", ", tags));
        return new SystemChatMessage(messageContent);
    }

    private async Task<UserChatMessage> CreateUserMessageAsync(string title, string text)
    {
        var messageTemplate = await LoadTextFromFileAsync("UserMessage.txt");
        var messageContent = messageTemplate
            .Replace("{title}", title)
            .Replace("{article}", text);
        return new UserChatMessage(messageContent);
    }

    private Task<string> LoadTextFromFileAsync(string filename) => File.ReadAllTextAsync(filename);
}
