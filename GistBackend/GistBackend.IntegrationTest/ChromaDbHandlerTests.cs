using GistBackend.Exceptions;
using GistBackend.Handler;
using GistBackend.Handler.ChromaDbHandler;
using GistBackend.Handler.OpenAiHandler;
using GistBackend.IntegrationTest.Utils;
using GistBackend.Types;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace GistBackend.IntegrationTest;

public class ChromaDbHandlerTests(ChromaDbFixture fixture) : IClassFixture<ChromaDbFixture>
{
    private readonly Random _random = new();
    private static readonly Dictionary<string, float[]> TestTextsAndEmbeddings = new() {
        { "test text", Enumerable.Repeat(0.1f, 100).ToArray() },
        { "very different test text", Enumerable.Repeat(0.9f, 100).ToArray() },
        { "very similar test text", Enumerable.Repeat(0.100000001f, 100).ToArray() },
    };

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
        var (text, embedding) = TestTextsAndEmbeddings.First();

        await handler.InsertEntryAsync(entry, text, CancellationToken.None);

        var expectedDocument = new Document([entry.Reference], [new Metadata(entry.Reference, entry.FeedId)])
            { Embeddings = [embedding] };
        await Asserter.AssertDocumentInDbAsync(expectedDocument);
    }

    [Fact]
    public async Task InsertEntryAsync_EntryIsAlreadyInDb_ThrowsDatabaseOperationException()
    {
        var handler = CreateChromaDbHandler();
        var entry = CreateTestEntry();
        var (text, _) = TestTextsAndEmbeddings.First();
        await handler.InsertEntryAsync(entry, text, CancellationToken.None);

        await Assert.ThrowsAsync<DatabaseOperationException>(() =>
            handler.InsertEntryAsync(entry, text, CancellationToken.None));
    }

    [Fact]
    public async Task InsertEntryAsync_ReferenceIsEmptyString_ThrowsArgumentException()
    {
        var handler = CreateChromaDbHandler();
        var entry = CreateTestEntry() with { Reference = "" };
        var (text, _) = TestTextsAndEmbeddings.First();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.InsertEntryAsync(entry, text, CancellationToken.None));
    }

    [Fact]
    public async Task InsertEntryAsync_ReferenceIsTooLong_ThrowsArgumentException()
    {
        var handler = CreateChromaDbHandler();
        var entry = CreateTestEntry() with { Reference = new string('A', 1000000) };
        var (text, _) = TestTextsAndEmbeddings.First();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.InsertEntryAsync(entry, text, CancellationToken.None));
    }

    [Fact]
    public async Task DisableEntryAsync_EntryIsInDbAndEnabled_EntryIsDisabled()
    {
        var handler = CreateChromaDbHandler();
        var entry = CreateTestEntry();
        var (text, embedding) = TestTextsAndEmbeddings.First();
        await handler.InsertEntryAsync(entry, text, CancellationToken.None);

        await handler.DisableEntryAsync(entry, CancellationToken.None);

        var expectedDocument = new Document([entry.Reference], [new Metadata(entry.Reference, entry.FeedId, true)])
            { Embeddings = [embedding] };
        await Asserter.AssertDocumentInDbAsync(expectedDocument);
    }

    [Fact]
    public async Task DisableEntryAsync_EntryIsInDbAndDisabled_EntryIsDisabled()
    {
        var handler = CreateChromaDbHandler();
        var entry = CreateTestEntry();
        var (text, embedding) = TestTextsAndEmbeddings.First();
        await handler.InsertEntryAsync(entry, text, CancellationToken.None);
        await handler.DisableEntryAsync(entry, CancellationToken.None);

        await handler.DisableEntryAsync(entry, CancellationToken.None);

        var expectedDocument = new Document([entry.Reference], [new Metadata(entry.Reference, entry.FeedId, true)])
            { Embeddings = [embedding] };
        await Asserter.AssertDocumentInDbAsync(expectedDocument);
    }

    [Fact]
    public async Task DisableEntryAsync_EntryDoesNotExist_ThrowsDatabaseOperationException()
    {
        var handler = CreateChromaDbHandler();
        var entry = CreateTestEntry();

        await Assert.ThrowsAsync<DatabaseOperationException>(() =>
            handler.DisableEntryAsync(entry, CancellationToken.None));
    }

    [Fact]
    public async Task DisableEntryAsync_ReferenceIsEmptyString_ThrowsArgumentException()
    {
        var handler = CreateChromaDbHandler();
        var entry = CreateTestEntry() with { Reference = "" };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.DisableEntryAsync(entry, CancellationToken.None));
    }

    [Fact]
    public async Task DisableEntryAsync_ReferenceIsTooLong_ThrowsArgumentException()
    {
        var handler = CreateChromaDbHandler();
        var entry = CreateTestEntry() with { Reference = new string('A', 1000000) };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.DisableEntryAsync(entry, CancellationToken.None));
    }

    [Fact]
    public async Task EnableEntryAsync_EntryIsInDbAndDisabled_EntryIsEnabled()
    {
        var handler = CreateChromaDbHandler();
        var entry = CreateTestEntry();
        var (text, embedding) = TestTextsAndEmbeddings.First();
        await handler.InsertEntryAsync(entry, text, CancellationToken.None);
        await handler.DisableEntryAsync(entry, CancellationToken.None);

        await handler.EnableEntryAsync(entry, CancellationToken.None);

        var expectedDocument = new Document([entry.Reference], [new Metadata(entry.Reference, entry.FeedId)])
            { Embeddings = [embedding] };
        await Asserter.AssertDocumentInDbAsync(expectedDocument);
    }

    [Fact]
    public async Task EnableEntryAsync_EntryIsInDbAndEnabled_EntryIsEnabled()
    {
        var handler = CreateChromaDbHandler();
        var entry = CreateTestEntry();
        var (text, embedding) = TestTextsAndEmbeddings.First();
        await handler.InsertEntryAsync(entry, text, CancellationToken.None);

        await handler.EnableEntryAsync(entry, CancellationToken.None);

        var expectedDocument = new Document([entry.Reference], [new Metadata(entry.Reference, entry.FeedId)])
            { Embeddings = [embedding] };
        await Asserter.AssertDocumentInDbAsync(expectedDocument);
    }

    [Fact]
    public async Task EnableEntryAsync_EntryDoesNotExist_ThrowsDatabaseOperationException()
    {
        var handler = CreateChromaDbHandler();
        var entry = CreateTestEntry();

        await Assert.ThrowsAsync<DatabaseOperationException>(() =>
            handler.EnableEntryAsync(entry, CancellationToken.None));
    }

    [Fact]
    public async Task EnableEntryAsync_ReferenceIsEmptyString_ThrowsArgumentException()
    {
        var handler = CreateChromaDbHandler();
        var entry = CreateTestEntry() with { Reference = "" };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.EnableEntryAsync(entry, CancellationToken.None));
    }

    [Fact]
    public async Task EnableEntryAsync_ReferenceIsTooLong_ThrowsArgumentException()
    {
        var handler = CreateChromaDbHandler();
        var entry = CreateTestEntry() with { Reference = new string('A', 1000000) };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.EnableEntryAsync(entry, CancellationToken.None));
    }

    [Fact]
    public async Task GetReferenceAndScoreOfSimilarEntriesAsync_LessEntriesInDb_ReturnsAllEntries()
    {
        var handler = CreateChromaDbHandler(_random.NextString());
        var entries = TestTextsAndEmbeddings.Select(_ => CreateTestEntry()).ToArray();
        const int nResults = 10;
        foreach (var ((text, _), entry) in TestTextsAndEmbeddings.Zip(entries))
        {
            await handler.InsertEntryAsync(entry, text, CancellationToken.None);
        }

        var actual = await handler.GetReferenceAndScoreOfSimilarEntriesAsync(entries.First().Reference,
            CancellationToken.None, nResults);

        Assert.True(actual.Length < nResults);
        Assert.Equal(entries.Length, actual.Length);
        Assert.Equivalent(entries.Select(entry => entry.Reference), actual.Select(doc => doc.Reference));
    }

    [Fact]
    public async Task GetReferenceAndScoreOfSimilarEntriesAsync_NotEnoughEntriesInDb_ReturnsOnlyNEntries()
    {
        var handler = CreateChromaDbHandler(_random.NextString());
        var entries = TestTextsAndEmbeddings.Select(_ => CreateTestEntry()).ToArray();
        const int nResults = 2;
        foreach (var ((text, _), entry) in TestTextsAndEmbeddings.Zip(entries))
        {
            await handler.InsertEntryAsync(entry, text, CancellationToken.None);
        }

        var actual = await handler.GetReferenceAndScoreOfSimilarEntriesAsync(entries.First().Reference,
            CancellationToken.None, nResults);

        Assert.Equal(nResults, actual.Length);
        foreach (var similarDocument in actual)
        {
            Assert.Contains(similarDocument.Reference, entries.Select(entry => entry.Reference));
        }
    }

    [Fact]
    public async Task GetReferenceAndScoreOfSimilarEntriesAsync_DisabledFeedIds_OnlyEntriesWithEnabledFeedIds()
    {
        var handler = CreateChromaDbHandler(_random.NextString());
        const int disabledFeedId = 1;
        const int enabledFeedId = 2;
        var entries = new[] {
            CreateTestEntry() with { FeedId = enabledFeedId },
            CreateTestEntry() with { FeedId = enabledFeedId },
            CreateTestEntry() with { FeedId = disabledFeedId }
        };
        foreach (var ((text, _), entry) in TestTextsAndEmbeddings.Zip(entries))
        {
            await handler.InsertEntryAsync(entry, text, CancellationToken.None);
        }

        var actual = await handler.GetReferenceAndScoreOfSimilarEntriesAsync(entries.First().Reference,
            CancellationToken.None, disabledFeedIds: [ disabledFeedId ]);

        Assert.Equal(2, actual.Length);
        Assert.Equivalent(
            entries.Where(entry => entry.FeedId == enabledFeedId).Select(entry => entry.Reference),
            actual.Select(entry => entry.Reference)
        );
    }

    [Fact]
    public async Task GetReferenceAndScoreOfSimilarEntriesAsync_DisabledEntriesInDb_OnlyEnabledEntries()
    {
        var handler = CreateChromaDbHandler(_random.NextString());
        var entries = TestTextsAndEmbeddings.Select(_ => CreateTestEntry()).ToArray();
        foreach (var ((text, _), entry) in TestTextsAndEmbeddings.Zip(entries))
        {
            await handler.InsertEntryAsync(entry, text, CancellationToken.None);
        }
        await handler.DisableEntryAsync(entries.Last(), CancellationToken.None);

        var actual = await handler.GetReferenceAndScoreOfSimilarEntriesAsync(entries.First().Reference,
            CancellationToken.None);

        Assert.Equal(2, actual.Length);
        Assert.Equivalent(
            entries.SkipLast(1).Select(entry => entry.Reference),
            actual.Select(entry => entry.Reference)
        );
    }

    private ChromaDbHandler CreateChromaDbHandler(string? collectionName = null)
    {
        var options = collectionName is null
            ? _handlerOptions
            : _handlerOptions with { GistsCollectionName = collectionName };
        return new ChromaDbHandler(CreateOpenAIHandlerMock(), new HttpClient(), Options.Create(options));
    }

    private static IOpenAIHandler CreateOpenAIHandlerMock()
    {
        var openAiHandlerMock = Substitute.For<IOpenAIHandler>();
        foreach (var (text, embedding) in TestTextsAndEmbeddings)
        {
            openAiHandlerMock.GenerateEmbeddingAsync(text, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(embedding));
        }
        return openAiHandlerMock;
    }

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
