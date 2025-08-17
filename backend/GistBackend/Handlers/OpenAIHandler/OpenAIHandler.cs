using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using GistBackend.Exceptions;
using GistBackend.Types;
using GistBackend.Utils;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using static GistBackend.Utils.LogEvents;

namespace GistBackend.Handlers.OpenAiHandler;

public interface IOpenAIHandler {
    public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct);
    public Task<SummaryAIResponse> GenerateSummaryTagsAndQueryAsync(string title, string text, CancellationToken ct);
    public Task<Recap> GenerateDailyRecapAsync(IEnumerable<Gist> gists, CancellationToken ct);
    public Task<Recap> GenerateWeeklyRecapAsync(IEnumerable<Gist> gists, CancellationToken ct);
}

public class OpenAIHandler(IEmbeddingClientHandler embeddingClientHandler, IChatClientHandler chatClientHandler,
    ILogger<OpenAIHandler>? logger = null) : IOpenAIHandler
{
    private readonly Lazy<Task<IEnumerable<string>>> _tags = new(LoadSummaryTagsAsync);
    private readonly Lazy<Task<ChatCompletionOptions>> _summaryChatCompletionOptions =
        new(LoadSummaryChatCompletionOptionsAsync);
    private readonly Lazy<Task<ChatCompletionOptions>> _recapChatCompletionOptions =
        new(LoadRecapChatCompletionOptionsAsync);

    private Task<IEnumerable<string>> GetTagsAsync() => _tags.Value;
    private Task<ChatCompletionOptions> GetSummaryChatCompletionOptionsAsync() => _summaryChatCompletionOptions.Value;
    private Task<ChatCompletionOptions> GetRecapChatCompletionOptionsAsync() => _recapChatCompletionOptions.Value;

    public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct) =>
        embeddingClientHandler.GenerateEmbeddingAsync(text, ct);

    public async Task<SummaryAIResponse> GenerateSummaryTagsAndQueryAsync(string title, string text, CancellationToken ct)
    {
        var messages = await CreateSummaryChatMessagesAsync(title, text, ct);
        var completionOptions = await GetSummaryChatCompletionOptionsAsync();
        var result = await chatClientHandler.CompleteChatAsync(messages, completionOptions, ct);
        try
        {
            var aiResponse = JsonSerializer.Deserialize<SummaryAIResponse>(result, SerializerDefaults.JsonOptions);
            if (aiResponse is null) throw new ExternalServiceException("Could not parse summary AI response");
            return aiResponse;
        }
        catch (JsonException e)
        {
            const string errorMessage = "Error when parsing the summary AI response JSON";
            logger?.LogError(SummaryAIResponseJsonParsingError, e, errorMessage);
            throw new ExternalServiceException(errorMessage, e);
        }
    }

    private async Task<IEnumerable<ChatMessage>> CreateSummaryChatMessagesAsync(string title, string text,
        CancellationToken ct)
    {
        return [
            await CreateSummarySystemMessageAsync(await GetTagsAsync(), ct),
            await CreateSummaryUserMessageAsync(title, text, ct)
        ];
    }

    private static async Task<SystemChatMessage> CreateSummarySystemMessageAsync(IEnumerable<string> tags,
        CancellationToken ct)
    {
        var messageTemplate = await LoadTextFromFileAsync("Summary", "SystemMessage.txt", ct);
        var nowDateString = DateTimeOffset.UtcNow.ToString("R", CultureInfo.InvariantCulture);
        var messageContent = messageTemplate
            .Replace("{now}", nowDateString)
            .Replace("{tags}", string.Join(", ", tags));
        return new SystemChatMessage(messageContent);
    }

    private static async Task<UserChatMessage> CreateSummaryUserMessageAsync(string title, string text,
        CancellationToken ct)
    {
        var messageTemplate = await LoadTextFromFileAsync("Summary", "UserMessage.txt", ct);
        var messageContent = messageTemplate
            .Replace("{title}", title)
            .Replace("{article}", text);
        return new UserChatMessage(messageContent);
    }

    private static async Task<IEnumerable<string>> LoadSummaryTagsAsync()
    {
        var tags = await JsonSerializer.DeserializeAsync<string[]>(
            File.OpenRead(GetPathToFileInOutputDirectory("Summary", "Tags.json")));
        if (tags is null) throw new Exception("Could not load and parse tags");
        return tags;
    }

    private static Task<ChatCompletionOptions> LoadSummaryChatCompletionOptionsAsync() =>
        LoadChatCompletionOptionsAsync("Summary", "news_article_key_take_aways");

    private static Task<ChatCompletionOptions> LoadRecapChatCompletionOptionsAsync() =>
        LoadChatCompletionOptionsAsync("Recap", "news_articles_recap");

    private static async Task<ChatCompletionOptions> LoadChatCompletionOptionsAsync(string directory, string name)
    {
        var responseSchema = await LoadTextFromFileAsync(directory, "ResponseSchema.json");
        var responseSchemaBytes = BinaryData.FromBytes(Encoding.UTF8.GetBytes(responseSchema));
        return new ChatCompletionOptions {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                name,
                responseSchemaBytes,
                jsonSchemaIsStrict: true
            )
        };
    }

    public Task<Recap> GenerateDailyRecapAsync(IEnumerable<Gist> gists, CancellationToken ct) =>
        GenerateRecapAsync(RecapType.Daily, gists, ct);

    public Task<Recap> GenerateWeeklyRecapAsync(IEnumerable<Gist> gists, CancellationToken ct) =>
        GenerateRecapAsync(RecapType.Weekly, gists, ct);

    private async Task<Recap> GenerateRecapAsync(RecapType recapType, IEnumerable<Gist> gists,
        CancellationToken ct)
    {
        var messages = await CreateRecapChatMessagesAsync(recapType, gists, ct);
        var completionOptions = await GetRecapChatCompletionOptionsAsync();
        var result = await chatClientHandler.CompleteChatAsync(messages, completionOptions, ct);
        try
        {
            var recap = JsonSerializer.Deserialize<Recap>(result, SerializerDefaults.JsonOptions);
            if (recap is null) throw new ExternalServiceException("Could not parse recap AI response");
            return recap;
        }
        catch (JsonException e)
        {
            const string errorMessage = "Error when parsing the recap AI response JSON";
            logger?.LogError(RecapAIResponseJsonParsingError, e, errorMessage);
            throw new ExternalServiceException(errorMessage, e);
        }
    }

    private static async Task<IEnumerable<ChatMessage>> CreateRecapChatMessagesAsync(RecapType recapType,
        IEnumerable<Gist> gists, CancellationToken ct)
    {
        var messages = new List<ChatMessage> {
            await CreateRecapSystemMessageAsync(recapType, ct),
            await CreateRecapUserMessageAsync(gists, ct)
        };
        return messages;
    }

    private static async Task<SystemChatMessage> CreateRecapSystemMessageAsync(RecapType recapType,
        CancellationToken ct)
    {
        var timeFrameDesc = recapType == RecapType.Daily ? "24 hours" : "7 days";
        var toTime = DateTimeOffset.UtcNow;
        var fromTime = toTime.AddDays(recapType == RecapType.Daily ? -1 : -7);
        var messageTemplate = await LoadTextFromFileAsync("Recap", "SystemMessage.txt", ct);
        var messageContent = messageTemplate
            .Replace("{timeframe_desc}", timeFrameDesc)
            .Replace("{from_time}", fromTime.ToString("R", CultureInfo.InvariantCulture))
            .Replace("{to_time}", toTime.ToString("R", CultureInfo.InvariantCulture));
        return new SystemChatMessage(messageContent);
    }

    private static async Task<UserChatMessage> CreateRecapUserMessageAsync(IEnumerable<Gist> gists,
        CancellationToken ct)
    {
        var messageTemplate = await LoadTextFromFileAsync("Recap", "UserMessage.txt", ct);
        var gistDescriptions = gists.Select(gist => messageTemplate
            .Replace("{title}", gist.Title)
            .Replace("{summary}", gist.Summary)
            .Replace("{id}", gist.Id.ToString())
        );
        var messageContent = string.Join("\n", gistDescriptions);
        return new UserChatMessage(messageContent);
    }

    private static Task<string> LoadTextFromFileAsync(string directoryName, string fileName,
        CancellationToken? ct = null) =>
        File.ReadAllTextAsync(GetPathToFileInOutputDirectory(directoryName, fileName), ct ?? CancellationToken.None);

    private static string GetPathToFileInOutputDirectory(string directoryName, string fileName)
    {
        var assembly = Assembly.GetAssembly(typeof(OpenAIHandler)) ?? throw new Exception("Could not get assembly");
        var directory = Path.GetDirectoryName(assembly.Location)
                        ?? throw new Exception("Could not get directory of assembly");
        return Path.Combine(directory, "Handlers", "OpenAIHandler", "Resources", directoryName, fileName);
    }
}
