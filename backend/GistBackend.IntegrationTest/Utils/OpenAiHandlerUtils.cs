using GistBackend.Handlers.OpenAiHandler;
using NSubstitute;
using static TestUtilities.TestData;

namespace GistBackend.IntegrationTest.Utils;

public static class OpenAiHandlerUtils
{
    public static IOpenAIHandler CreateOpenAIHandlerMock()
    {
        var openAiHandlerMock = Substitute.For<IOpenAIHandler>();
        foreach (var (text, embedding) in TestTextsAndEmbeddings)
        {
            openAiHandlerMock.GenerateEmbeddingAsync(text, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(embedding));
        }
        return openAiHandlerMock;
    }
}
