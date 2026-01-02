using GistBackend.Handlers.AIHandler;
using Microsoft.Extensions.Options;
using NSubstitute;
using SharpToken;

namespace GistBackend.UnitTest;

public class AIHandlerTests
{
    private const string TestInputBase = "This is a short input with more than 10 tokens I hope.";

    [Theory]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(5000)]
    [InlineData(7500)]
    public async Task GenerateEmbeddingAsync_ShortInput_UsesFullInputForEmbedding(int testTokenCount)
    {
        var embeddingClientHandlerMock = CreateEmbeddingClientHandlerMock();
        var aiHandler = CreateAIHandler(embeddingClientHandlerMock);
        var encoding = GptEncoding.GetEncodingForModel(embeddingClientHandlerMock.Model);
        var expectedTokens = encoding.Encode(TestInputBase).ToList();
        Assert.True(TestInputBase.Length >= 10);
        var testInputTokens = Enumerable.Range(0, testTokenCount / 10).Select(_ => expectedTokens.Take(10))
            .SelectMany(x => x).ToList();
        var testInput = encoding.Decode(testInputTokens);

        await aiHandler.GenerateEmbeddingAsync(testInput, CancellationToken.None);

        await embeddingClientHandlerMock.Received(1).GenerateEmbeddingAsync(testInput, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_LongerInput_UsesOnlyFirst7500TokensForEmbedding()
    {
        var embeddingClientHandlerMock = CreateEmbeddingClientHandlerMock();
        var aiHandler = CreateAIHandler(embeddingClientHandlerMock);
        var encoding = GptEncoding.GetEncodingForModel(embeddingClientHandlerMock.Model);
        var expectedTokens = encoding.Encode(TestInputBase).ToList();
        Assert.True(TestInputBase.Length >= 10);
        const int testTokenCount = 10000;
        // Generate an input with exactly 10000 tokens by repeating the first 10 tokens.
        var testInputTokens = Enumerable.Range(0, testTokenCount / 10).Select(_ => expectedTokens.Take(10))
            .SelectMany(x => x).ToList();
        var testInput = encoding.Decode(testInputTokens);
        var expectedTruncatedInput = encoding.Decode(testInputTokens.Take(7500));

        await aiHandler.GenerateEmbeddingAsync(testInput, CancellationToken.None);

        await embeddingClientHandlerMock.Received(1)
            .GenerateEmbeddingAsync(expectedTruncatedInput, Arg.Any<CancellationToken>());
    }

    private static IEmbeddingClientHandler CreateEmbeddingClientHandlerMock()
    {
        var embeddingClientHandlerMock = Substitute.For<IEmbeddingClientHandler>();
        embeddingClientHandlerMock.Model.Returns(new EmbeddingClientHandlerOptions().Model);
        return embeddingClientHandlerMock;
    }

    private static AIHandler CreateAIHandler(IEmbeddingClientHandler embeddingClientHandler) =>
        new (embeddingClientHandler, new HttpClient(), Options.Create(new AIHandlerOptions()));
}
