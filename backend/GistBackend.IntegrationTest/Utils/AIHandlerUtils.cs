using GistBackend.Handlers.AIHandler;
using NSubstitute;
using static TestUtilities.TestData;

namespace GistBackend.IntegrationTest.Utils;

public static class AIHandlerUtils
{
    public static IAIHandler CreateOpenAIHandlerMock()
    {
        var openAiHandlerMock = Substitute.For<IAIHandler>();
        foreach (var (text, embedding) in TestTextsAndEmbeddings)
        {
            openAiHandlerMock.GenerateEmbeddingAsync(text, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(embedding));
        }
        return openAiHandlerMock;
    }
}
