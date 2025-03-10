using System.Text.Json;
using GistBackend.Exceptions;
using GistBackend.Handler.OpenAiHandler;
using GistBackend.Types;
using NSubstitute;
using OpenAI.Chat;
using JsonException = Newtonsoft.Json.JsonException;

namespace GistBackend.UnitTest;

public class OpenAIHandlerTests
{
    private readonly AIResponse _testAiResponse =
        new("test summary", ["test tag 1", "test tag 2", "test tag 3"], "test search query");
    private const string TestTitle = "test title";
    private const string TestText = "test text";
    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
        { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    [Fact]
    public async Task GenerateSummaryTagsAndQueryAsync_ValidResponseFromAPI_ExpectedAIResponseParsed()
    {
        var ct = CancellationToken.None;
        var chatClientHandler = CreateChatClientHandler(ct);
        var handler = new OpenAIHandler(Substitute.For<IEmbeddingClientHandler>(), chatClientHandler);

        var actual = await handler.GenerateSummaryTagsAndQueryAsync(TestTitle, TestText, ct);

        Assert.Equivalent(_testAiResponse, actual);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{\"some value\"}")]
    [InlineData("{ \"summary\": \"text\", \"tags\": [\"tag1\", \"tag2\"], \"query\": \"bad \" quote\" }")]
    public async Task GenerateSummaryTagsAndQueryAsync_InvalidJsonResponseFromAPI_ThrowsExternalServiceException(
        string responseText)
    {
        var ct = CancellationToken.None;
        var chatClientHandler = CreateChatClientHandler(ct, responseText);
        var handler = new OpenAIHandler(Substitute.For<IEmbeddingClientHandler>(), chatClientHandler);

        await Assert.ThrowsAsync<ExternalServiceException>(() =>
            handler.GenerateSummaryTagsAndQueryAsync(TestTitle, TestText, ct));
    }

    private IChatClientHandler CreateChatClientHandler(CancellationToken ct, string? responseText = null)
    {
        var chatClientHandlerMock = Substitute.For<IChatClientHandler>();
        chatClientHandlerMock.CompleteChatAsync(
            Arg.Is<IEnumerable<ChatMessage>>(messages => AreExpectedMessages(messages)),
            Arg.Any<ChatCompletionOptions>(),
            ct
        ).Returns(Task.FromResult(responseText ?? GetResponseText()));
        return chatClientHandlerMock;
    }

    private static bool AreExpectedMessages(IEnumerable<ChatMessage> messages)
    {
        messages = messages.ToArray();
        return messages.Count() == 2
               && messages.First().GetType() == typeof(SystemChatMessage)
               && messages.First().Content.Single().Text
                   .StartsWith("You are an extremely experienced IT security news analyst.")
               && messages.Last().GetType() == typeof(UserChatMessage)
               && messages.Last().Content.Single().Text.StartsWith("TITLE:");
    }

    private string GetResponseText()
    {
        var responseText = JsonSerializer.Serialize(_testAiResponse, _jsonSerializerOptions);
        Assert.NotNull(responseText);
        return responseText;
    }
}
