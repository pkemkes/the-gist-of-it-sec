using GistBackend.Exceptions;
using GistBackend.Handler;
using GistBackend.Handler.ChromaDbHandler;
using GistBackend.IntegrationTest.Utils;
using GistBackend.Types;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace GistBackend.IntegrationTest;

public class ChromaDbHandlerTests(ChromaDbFixture fixture) : IClassFixture<ChromaDbFixture>
{
    private readonly Random _random = new();
    private static readonly float[] TestEmbeddings = [0.1f, 0.2f, 0.3f, 0.4f];

    private readonly ChromaDbHandlerOptions _handlerOptions = new(
        fixture.Hostname,
        ChromaDbFixture.GistServiceServerAuthnCredentials,
        fixture.ExposedPort
    );

    private ChromaDbAsserter Asserter => new(_handlerOptions);

    [Fact]
    public async Task InsertEntryAsync_EntryNotInChromaDb_EntryInserted()
    {
        var handler = CreateChromaDbHandler();
        var entry = CreateTestEntry();

        await handler.InsertEntryAsync(entry, "test text", CancellationToken.None);

        await Asserter.AssertEmbeddingsInDbAsync(entry.Reference, TestEmbeddings);
    }

    [Fact]
    public async Task InsertEntryAsync_EntryIsAlreadyInDb_ThrowsDatabaseOperationException()
    {
        var handler = CreateChromaDbHandler();
        var entry = CreateTestEntry();
        await handler.InsertEntryAsync(entry, "test text", CancellationToken.None);

        await Assert.ThrowsAsync<DatabaseOperationException>(() =>
            handler.InsertEntryAsync(entry, "test text", CancellationToken.None));
    }

    private static IOpenAIHandler CreateOpenAIHandlerMock()
    {
        var openAiHandlerMock = Substitute.For<IOpenAIHandler>();
        openAiHandlerMock.GenerateEmbeddingsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TestEmbeddings));
        return openAiHandlerMock;
    }

    private ChromaDbHandler CreateChromaDbHandler() =>
        new(CreateOpenAIHandlerMock(), new HttpClient(), Options.Create(_handlerOptions));

    private RssEntry CreateTestEntry() => new(
        _random.NextString(),
        _random.Next(),
        _random.NextString(),
        _random.NextString(),
        _random.NextDateTime(max: DateTime.UnixEpoch.AddYears(30)),
        _random.NextDateTime(min: DateTime.UnixEpoch.AddYears(30)),
        _random.NextString(),
        [_random.NextString(), _random.NextString(), _random.NextString()],
        text => text
    );
}
