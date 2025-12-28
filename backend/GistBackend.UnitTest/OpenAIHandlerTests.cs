using System.Text.Json;
using GistBackend.Exceptions;
using GistBackend.Handlers.OpenAiHandler;
using GistBackend.Types;
using GistBackend.Utils;
using NSubstitute;
using OpenAI.Chat;

namespace GistBackend.UnitTest;

public class OpenAIHandlerTests
{
    private readonly SummaryAIResponse _testAIResponse =
        new("test summary english", "test summary german", "test title translated",
            ["test tag 1", "test tag 2", "test tag 3"], "test search query");

    private const Language TestLanguage = Language.De;
    private const string TestTitle = "test title";
    private const string TestText = "test text";
    private readonly RecapAIResponse _testRecapAIResponse = new([
        new RecapSection("first en test heading", "first en test recap", new List<int> { 11, 12, 13 }),
        new RecapSection("second en test heading", "second en test recap", new List<int> { 21, 22, 23 })
    ], [
        new RecapSection("first de test heading", "first de test recap", new List<int> { 11, 12, 13 }),
        new RecapSection("second de test heading", "second de test recap", new List<int> { 21, 22, 23 })
    ]);
    private readonly List<ConstructedGist> _testGists = [
        new(
            1,
            "test reference",
            "test feed title",
            "https://test.feed-url.com/",
            "test title",
            "test author",
            "https://test.url.com/",
            new DateTime().ToDatabaseCompatibleString(),
            new DateTime().ToDatabaseCompatibleString(),
            "test summary",
            ["test tag 1", "test tag 2"],
            "test search query"
        ),

        new(
            2,
            "other test reference",
            "test feed title",
            "https://test.feed-url.com/",
            "other test title",
            "other test author",
            "https://test.url.com/other",
            new DateTime().ToDatabaseCompatibleString(),
            new DateTime().ToDatabaseCompatibleString(),
            "other test summary",
            ["other test tag 1", "other test tag 2"],
            "other test search query"
        )
    ];

    [Fact]
    public async Task GenerateSummaryTagsAndQueryAsync_ValidResponseFromAPI_ExpectedAIResponseParsed()
    {
        var ct = CancellationToken.None;
        var chatClientHandler = CreateSummaryChatClientHandler(GetResponseText(_testAIResponse), ct);
        var handler = new OpenAIHandler(Substitute.For<IEmbeddingClientHandler>(), chatClientHandler);

        var actual = await handler.GenerateSummaryAIResponseAsync(TestLanguage, TestTitle, TestText, ct);

        Assert.Equivalent(_testAIResponse, actual);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{\"some value\"}")]
    [InlineData("{ \"summary\": \"text\", \"tags\": [\"tag1\", \"tag2\"], \"query\": \"bad \" quote\" }")]
    public async Task GenerateSummaryTagsAndQueryAsync_InvalidJsonResponseFromAPI_ThrowsExternalServiceException(
        string responseText)
    {
        var ct = CancellationToken.None;
        var chatClientHandler = CreateSummaryChatClientHandler(responseText, ct);
        var handler = new OpenAIHandler(Substitute.For<IEmbeddingClientHandler>(), chatClientHandler);

        await Assert.ThrowsAsync<ExternalServiceException>(() =>
            handler.GenerateSummaryAIResponseAsync(TestLanguage, TestTitle, TestText, ct));
    }

    [Fact]
    public async Task GenerateDailyRecapAsync_ValidResponseFromAPI_ExpectedAIResponseParsed()
    {
        var ct = CancellationToken.None;
        var chatClientHandler = CreateRecapChatClientHandler(GetResponseText(_testRecapAIResponse), RecapType.Daily, ct);
        var handler = new OpenAIHandler(Substitute.For<IEmbeddingClientHandler>(), chatClientHandler);

        var actual = await handler.GenerateDailyRecapAsync(_testGists, ct);

        Assert.NotNull(actual);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{\"some value\"}")]
    [InlineData("[{\"heading\": \"test heading\", \"recap\": \"bad \" quote\", \"related\": [1, 2, 3]}]")]
    public async Task GenerateDailyRecapAsync_InvalidJsonResponseFromAPI_ThrowsExternalServiceException(
        string responseText)
    {
        var ct = CancellationToken.None;
        var chatClientHandler = CreateRecapChatClientHandler(responseText, RecapType.Daily, ct);
        var handler = new OpenAIHandler(Substitute.For<IEmbeddingClientHandler>(), chatClientHandler);

        await Assert.ThrowsAsync<ExternalServiceException>(() => handler.GenerateDailyRecapAsync(_testGists, ct));
    }

    [Fact]
    public async Task GenerateWeeklyRecapAsync_ValidResponseFromAPI_ExpectedAIResponseParsed()
    {
        var ct = CancellationToken.None;
        var chatClientHandler = CreateRecapChatClientHandler(GetResponseText(_testRecapAIResponse), RecapType.Weekly, ct);
        var handler = new OpenAIHandler(Substitute.For<IEmbeddingClientHandler>(), chatClientHandler);

        var actual = await handler.GenerateWeeklyRecapAsync(_testGists, ct);

        Assert.NotNull(actual);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{\"some value\"}")]
    [InlineData("[{\"heading\": \"test heading\", \"recap\": \"bad \" quote\", \"related\": [1, 2, 3]}]")]
    public async Task GenerateWeeklyRecapAsync_InvalidJsonResponseFromAPI_ThrowsExternalServiceException(
        string responseText)
    {
        var ct = CancellationToken.None;
        var chatClientHandler = CreateRecapChatClientHandler(responseText, RecapType.Weekly, ct);
        var handler = new OpenAIHandler(Substitute.For<IEmbeddingClientHandler>(), chatClientHandler);

        await Assert.ThrowsAsync<ExternalServiceException>(() => handler.GenerateWeeklyRecapAsync(_testGists, ct));
    }

    private static IChatClientHandler CreateSummaryChatClientHandler(string responseText, CancellationToken ct)
    {
        var chatClientHandlerMock = Substitute.For<IChatClientHandler>();
        chatClientHandlerMock.CompleteChatAsync(
            Arg.Is<List<ChatMessage>>(messages => AreExpectedSummaryMessages(messages)),
            Arg.Any<ChatCompletionOptions>(),
            ct
        ).Returns(Task.FromResult(responseText));
        return chatClientHandlerMock;
    }

    private static bool AreExpectedSummaryMessages(List<ChatMessage> messages) =>
        messages.Count == 2
        && messages.First().GetType() == typeof(SystemChatMessage)
        && messages.First().Content.Single().Text
            .StartsWith(
                "You are an extremely experienced IT security news analyst. " +
                "The user will send you a message with a TITLE and an ARTICLE."
            )
        && messages.Last().GetType() == typeof(UserChatMessage)
        && messages.Last().Content.Single().Text.StartsWith("ORIGINAL LANGUAGE:");

    private IChatClientHandler CreateRecapChatClientHandler(string responseText, RecapType recapType,
        CancellationToken ct)
    {
        var chatClientHandlerMock = Substitute.For<IChatClientHandler>();
        chatClientHandlerMock.CompleteChatAsync(
            Arg.Is<List<ChatMessage>>(messages => AreExpectedRecapMessages(messages, recapType)),
            Arg.Any<ChatCompletionOptions>(),
            ct
        ).Returns(Task.FromResult(responseText));
        return chatClientHandlerMock;
    }

    private bool AreExpectedRecapMessages(List<ChatMessage> messages, RecapType recapType)
    {
        var timeFrameDesc = recapType == RecapType.Daily ? "24 hours" : "7 days";
        return messages.Count == 2
               && messages.First().GetType() == typeof(SystemChatMessage)
               && messages.First().Content.Single().Text
                   .StartsWith(
                       "You are an extremely experienced IT security news analyst. " +
                       $"The user will send you a list of news summaries from the last {timeFrameDesc}."
                   )
               && messages.Last().GetType() == typeof(UserChatMessage)
               && messages.Last().Content.Single().Text.StartsWith($"TITLE: {_testGists.First().Title}");
    }

    private static string GetResponseText(object objectToSerialize)
    {
        var responseText = JsonSerializer.Serialize(objectToSerialize, SerializerDefaults.JsonOptions);
        Assert.NotNull(responseText);
        return responseText;
    }
}
