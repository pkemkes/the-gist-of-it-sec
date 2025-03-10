using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using GistBackend.Exceptions;
using GistBackend.Types;
using GistBackend.Utils;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace GistBackend.Handler.OpenAiHandler;

public interface IOpenAIHandler {
    public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct);
    public Task<AIResponse> GenerateSummaryTagsAndQueryAsync(string title, string text, CancellationToken ct);
}

public class OpenAIHandler(IEmbeddingClientHandler embeddingClientHandler, IChatClientHandler chatClientHandler,
    ILogger<OpenAIHandler>? logger = null) : IOpenAIHandler
{
    private readonly Lazy<Task<IEnumerable<string>>> _tags = new(LoadTagsAsync);
    private readonly Lazy<Task<ChatCompletionOptions>> _chatCompletionOptions = new(LoadChatCompletionOptionsAsync);

    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
        { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    private Task<IEnumerable<string>> GetTagsAsync() => _tags.Value;
    private Task<ChatCompletionOptions> GetChatCompletionOptionsAsync() => _chatCompletionOptions.Value;

    public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct) =>
        embeddingClientHandler.GenerateEmbeddingAsync(text, ct);

    public async Task<AIResponse> GenerateSummaryTagsAndQueryAsync(string title, string text, CancellationToken ct)
    {
        var messages = await CreateChatMessagesAsync(title, text, ct);
        var result = await chatClientHandler.CompleteChatAsync(messages, await GetChatCompletionOptionsAsync(), ct);
        try
        {
            var aiResponse = JsonSerializer.Deserialize<AIResponse>(result, _jsonSerializerOptions);
            if (aiResponse is null) throw new ExternalServiceException("Could not parse AI response");
            return aiResponse;
        }
        catch (JsonException e)
        {
            const string errorMessage = "Error when parsing the AI response JSON";
            logger?.LogError(LogEvents.AIResponseJsonParsingError, e, errorMessage);
            throw new ExternalServiceException(errorMessage, e);
        }
    }

    private async Task<IEnumerable<ChatMessage>> CreateChatMessagesAsync(string title, string text,
        CancellationToken ct)
    {
        return [
            await CreateSystemMessageAsync(await GetTagsAsync(), ct),
            await CreateUserMessageAsync(title, text, ct)
        ];
    }

    private static async Task<SystemChatMessage> CreateSystemMessageAsync(IEnumerable<string> tags, CancellationToken ct)
    {
        var messageTemplate = await LoadTextFromFileAsync("SystemMessage.txt", ct);
        var nowDateString = DateTimeOffset.UtcNow.ToString("R", CultureInfo.InvariantCulture);
        var messageContent = messageTemplate
            .Replace("{now}", nowDateString)
            .Replace("{tags}", string.Join(", ", tags));
        return new SystemChatMessage(messageContent);
    }

    private static async Task<UserChatMessage> CreateUserMessageAsync(string title, string text, CancellationToken ct)
    {
        var messageTemplate = await LoadTextFromFileAsync("UserMessage.txt", ct);
        var messageContent = messageTemplate
            .Replace("{title}", title)
            .Replace("{article}", text);
        return new UserChatMessage(messageContent);
    }

    private static async Task<IEnumerable<string>> LoadTagsAsync()
    {
        var tags = await JsonSerializer.DeserializeAsync<string[]>(
            File.OpenRead(GetPathToFileInOutputDirectory("Tags.json")));
        if (tags is null) throw new Exception("Could not load and parse tags");
        return tags;
    }

    private static async Task<ChatCompletionOptions> LoadChatCompletionOptionsAsync()
    {
        var responseSchema = await LoadTextFromFileAsync("ResponseSchema.json");
        var responseSchemaBytes = BinaryData.FromBytes(Encoding.UTF8.GetBytes(responseSchema));
        return new ChatCompletionOptions {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                "news_article_key_take_aways",
                responseSchemaBytes,
                jsonSchemaIsStrict: true
            )
        };
    }

    private static Task<string> LoadTextFromFileAsync(string filename, CancellationToken? ct = null) =>
        File.ReadAllTextAsync(GetPathToFileInOutputDirectory(filename), ct ?? CancellationToken.None);

    private static string GetPathToFileInOutputDirectory(string filename)
    {
        var assembly = Assembly.GetAssembly(typeof(OpenAIHandler)) ?? throw new Exception("Could not get assembly");
        var directory = Path.GetDirectoryName(assembly.Location)
                        ?? throw new Exception("Could not get directory of assembly");
        return Path.Combine(directory, "Handler", "OpenAIHandler", "Resources", filename);
    }
}
