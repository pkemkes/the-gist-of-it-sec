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
    private readonly SummaryAIResponse _testSummaryAIResponse =
        new("test summary", ["test tag 1", "test tag 2", "test tag 3"], "test search query");
    private const string TestTitle = "test title";
    private const string TestText = "test text";
    private readonly Recap _testRecap = new([
        new RecapSection("first test heading", "first test recap", new List<int> { 11, 12, 13 }),
        new RecapSection("second test heading", "second test recap", new List<int> { 21, 22, 23 })
    ]);
    private readonly List<Gist> _testGists = [
        new(
            "test reference",
            1,
            "test author",
            "test title",
            new DateTime(),
            new DateTime(),
            new Uri("https://test.url.com/"),
            "test summary",
            "test tag 1;;test tag 2",
            "test search query",
            1
        ),

        new(
            "other test reference",
            1,
            "other test author",
            "other test title",
            new DateTime(),
            new DateTime(),
            new Uri("https://other.test.url.com/"),
            "other test summary",
            "test tag 3;;test tag 4",
            "other test search query",
            1
        )
    ];

    [Fact]
    public async Task GenerateSummaryTagsAndQueryAsync_ValidResponseFromAPI_ExpectedAIResponseParsed()
    {
        var ct = CancellationToken.None;
        var chatClientHandler = CreateSummaryChatClientHandler(GetResponseText(_testSummaryAIResponse), ct);
        var handler = new OpenAIHandler(Substitute.For<IEmbeddingClientHandler>(), chatClientHandler);

        var actual = await handler.GenerateSummaryTagsAndQueryAsync(TestTitle, TestText, ct);

        Assert.Equivalent(_testSummaryAIResponse, actual);
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
            handler.GenerateSummaryTagsAndQueryAsync(TestTitle, TestText, ct));
    }

    [Fact]
    public async Task GenerateDailyRecapAsync_ValidResponseFromAPI_ExpectedAIResponseParsed()
    {
        var ct = CancellationToken.None;
        var chatClientHandler = CreateRecapChatClientHandler(GetResponseText(_testRecap), RecapType.Daily, ct);
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
        var chatClientHandler = CreateRecapChatClientHandler(GetResponseText(_testRecap), RecapType.Weekly, ct);
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
            Arg.Is<IEnumerable<ChatMessage>>(messages => AreExpectedSummaryMessages(messages)),
            Arg.Any<ChatCompletionOptions>(),
            ct
        ).Returns(Task.FromResult(responseText));
        return chatClientHandlerMock;
    }

    private static bool AreExpectedSummaryMessages(IEnumerable<ChatMessage> messages)
    {
        messages = messages.ToArray();
        return messages.Count() == 2
               && messages.First().GetType() == typeof(SystemChatMessage)
               && messages.First().Content.Single().Text
                   .StartsWith(
                       "You are an extremely experienced IT security news analyst. " +
                       "The user will send you a message with a TITLE and an ARTICLE."
                   )
               && messages.Last().GetType() == typeof(UserChatMessage)
               && messages.Last().Content.Single().Text.StartsWith("TITLE:");
    }

    private IChatClientHandler CreateRecapChatClientHandler(string responseText, RecapType recapType,
        CancellationToken ct)
    {
        var chatClientHandlerMock = Substitute.For<IChatClientHandler>();
        chatClientHandlerMock.CompleteChatAsync(
            Arg.Is<IEnumerable<ChatMessage>>(messages => AreExpectedRecapMessages(messages, recapType)),
            Arg.Any<ChatCompletionOptions>(),
            ct
        ).Returns(Task.FromResult(responseText));
        return chatClientHandlerMock;
    }

    private bool AreExpectedRecapMessages(IEnumerable<ChatMessage> messages, RecapType recapType)
    {
        messages = messages.ToArray();
        var timeFrameDesc = recapType == RecapType.Daily ? "24 hours" : "7 days";
        return messages.Count() == 2
               && messages.First().GetType() == typeof(SystemChatMessage)
               && messages.First().Content.Single().Text
                   .StartsWith(
                       "You are an extremely experienced IT security news analyst. " +
                       $"The user will send you a list of news summaries from the last {timeFrameDesc}."
                   )
               && messages.Last().GetType() == typeof(UserChatMessage)
               && messages.Last().Content.Single().Text.StartsWith($"TITLE: {_testGists.First().Title}");
    }

    private string GetResponseText(object objectToSerialize)
    {
        var responseText = JsonSerializer.Serialize(objectToSerialize, SerializerDefaults.JsonOptions);
        Assert.NotNull(responseText);
        return responseText;
    }
}
