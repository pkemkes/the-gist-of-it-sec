using GistBackend.Exceptions;
using GistBackend.Handlers.ChromaDbHandler;
using GistBackend.IntegrationTest.Utils;
using GistBackend.Types;
using Microsoft.Extensions.Options;
using TestUtilities;
using static GistBackend.IntegrationTest.Utils.AIHandlerUtils;
using static TestUtilities.TestData;

namespace GistBackend.IntegrationTest;

public class ChromaDbHandlerTests(ChromaDbFixture fixture) : IClassFixture<ChromaDbFixture>
{
    private readonly Random _random = new();

    private static readonly SummaryAIResponse TestSummaryAIResponse = new(
        "test summary english",
        "test summary german",
        "translated test title",
        ["test tag 1", "test tag 2", "test tag 3"]
    );

    private readonly ChromaDbHandlerOptions _handlerOptions = new()
    {
        Server = fixture.Hostname,
        ServerAuthnCredentials = ChromaDbFixture.GistServiceServerAuthnCredentials,
        Port = fixture.ExposedPort
    };

    private ChromaDbAsserter Asserter => new(_handlerOptions);

    [Fact]
    public async Task UpsertEntryAsync_EntryNotInChromaDb_EntryInserted()
    {
        var handler = CreateChromaDbHandler();
        var entry = CreateTestEntry();
        var (text, embedding) = TestTextsAndEmbeddings.First();

        await handler.UpsertEntryAsync(entry, text, CancellationToken.None);

        var expectedDocument = new Document([entry.Reference], [new Metadata(entry.Reference, entry.FeedId)])
            { Embeddings = [embedding] };
        await Asserter.AssertDocumentInDbAsync(expectedDocument);
    }

    [Fact]
    public async Task UpsertEntryAsync_EntryIsAlreadyInDb_EntryUpdated()
    {
        var handler = CreateChromaDbHandler();
        var oldEntry = CreateTestEntry();
        var (oldText, _) = TestTextsAndEmbeddings.First();
        await handler.UpsertEntryAsync(oldEntry, oldText, CancellationToken.None);
        var newEntry = CreateTestEntry() with { Reference = oldEntry.Reference};
        var (newText, newEmbedding) = TestTextsAndEmbeddings.Skip(1).First();

        await handler.UpsertEntryAsync(newEntry, newText, CancellationToken.None);

        var expectedDocument = new Document([newEntry.Reference], [new Metadata(newEntry.Reference, newEntry.FeedId)])
            { Embeddings = [newEmbedding] };
        await Asserter.AssertDocumentInDbAsync(expectedDocument);
    }

    [Fact]
    public async Task UpsertEntryAsync_ReferenceIsEmptyString_ThrowsArgumentException()
    {
        var handler = CreateChromaDbHandler();
        var entry = CreateTestEntry() with { Reference = "" };
        var (text, _) = TestTextsAndEmbeddings.First();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.UpsertEntryAsync(entry, text, CancellationToken.None));
    }

    [Fact]
    public async Task UpsertEntryAsync_ReferenceIsTooLong_ThrowsArgumentException()
    {
        var handler = CreateChromaDbHandler();
        var entry = CreateTestEntry() with { Reference = new string('A', 1000000) };
        var (text, _) = TestTextsAndEmbeddings.First();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.UpsertEntryAsync(entry, text, CancellationToken.None));
    }

    [Fact]
    public async Task EnsureGistHasCorrectMetadataAsync_EntryIsInDbAndEnabledAndShouldBeDisabled_FalseAndEntryIsDisabled()
    {
        var handler = CreateChromaDbHandler();
        var entry = CreateTestEntry();
        var (text, embedding) = TestTextsAndEmbeddings.First();
        await handler.UpsertEntryAsync(entry, text, CancellationToken.None);
        var gist = new Gist(entry, TestSummaryAIResponse);

        var actual = await handler.EnsureGistHasCorrectMetadataAsync(gist, true, CancellationToken.None);

        var expectedDocument = new Document([entry.Reference], [new Metadata(entry.Reference, entry.FeedId, true)])
            { Embeddings = [embedding] };
        await Asserter.AssertDocumentInDbAsync(expectedDocument);
        Assert.False(actual);
    }

    [Fact]
    public async Task EnsureGistHasCorrectMetadataAsync_EntryIsInDbAndDisabledAndShouldBeDisabled_TrueAndEntryIsDisabled()
    {
        var handler = CreateChromaDbHandler();
        var entry = CreateTestEntry();
        var (text, embedding) = TestTextsAndEmbeddings.First();
        await handler.UpsertEntryAsync(entry, text, CancellationToken.None);
        var gist = new Gist(entry, TestSummaryAIResponse);
        await handler.EnsureGistHasCorrectMetadataAsync(gist, true, CancellationToken.None);

        var actual = await handler.EnsureGistHasCorrectMetadataAsync(gist, true, CancellationToken.None);

        var expectedDocument = new Document([entry.Reference], [new Metadata(entry.Reference, entry.FeedId, true)])
            { Embeddings = [embedding] };
        await Asserter.AssertDocumentInDbAsync(expectedDocument);
        Assert.True(actual);
    }

    [Fact]
    public async Task EnsureGistHasCorrectMetadataAsync_EntryIsInDbAndDisabledAndShouldBeEnabled_FalseAndEntryIsEnabled()
    {
        var handler = CreateChromaDbHandler();
        var entry = CreateTestEntry();
        var (text, embedding) = TestTextsAndEmbeddings.First();
        await handler.UpsertEntryAsync(entry, text, CancellationToken.None);
        var gist = new Gist(entry, TestSummaryAIResponse);
        await handler.EnsureGistHasCorrectMetadataAsync(gist, true, CancellationToken.None);

        var actual = await handler.EnsureGistHasCorrectMetadataAsync(gist, false, CancellationToken.None);

        var expectedDocument = new Document([entry.Reference], [new Metadata(entry.Reference, entry.FeedId)])
            { Embeddings = [embedding] };
        await Asserter.AssertDocumentInDbAsync(expectedDocument);
        Assert.False(actual);
    }

    [Fact]
    public async Task EnsureGistHasCorrectMetadataAsync_EntryIsInDbAndEnabledAndShouldBeEnabled_TrueAndEntryIsEnabled()
    {
        var handler = CreateChromaDbHandler();
        var entry = CreateTestEntry();
        var (text, embedding) = TestTextsAndEmbeddings.First();
        await handler.UpsertEntryAsync(entry, text, CancellationToken.None);
        var gist = new Gist(entry, TestSummaryAIResponse);

        var actual = await handler.EnsureGistHasCorrectMetadataAsync(gist, false, CancellationToken.None);

        var expectedDocument = new Document([entry.Reference], [new Metadata(entry.Reference, entry.FeedId)])
            { Embeddings = [embedding] };
        await Asserter.AssertDocumentInDbAsync(expectedDocument);
        Assert.True(actual);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EnsureGistHasCorrectMetadataAsync_EntryDoesNotExist_ThrowsDatabaseOperationException(bool disabled)
    {
        var handler = CreateChromaDbHandler();
        var entry = CreateTestEntry();
        var gist = new Gist(entry, TestSummaryAIResponse);

        await Assert.ThrowsAsync<DatabaseOperationException>(() =>
            handler.EnsureGistHasCorrectMetadataAsync(gist, disabled, CancellationToken.None));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EnsureGistHasCorrectMetadataAsync_ReferenceIsEmptyString_ThrowsArgumentException(bool disabled)
    {
        var handler = CreateChromaDbHandler();
        var entry = CreateTestEntry() with { Reference = "" };
        var gist = new Gist(entry, TestSummaryAIResponse);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.EnsureGistHasCorrectMetadataAsync(gist, disabled, CancellationToken.None));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EnsureGistHasCorrectMetadataAsync_ReferenceIsTooLong_ThrowsArgumentException(bool disabled)
    {
        var handler = CreateChromaDbHandler();
        var entry = CreateTestEntry() with { Reference = new string('A', 1000000) };
        var gist = new Gist(entry, TestSummaryAIResponse);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.EnsureGistHasCorrectMetadataAsync(gist, disabled, CancellationToken.None));
    }

    [Fact]
    public async Task GetReferenceAndScoreOfSimilarEntriesAsync_LessEntriesInDb_ReturnsAllEntries()
    {
        var handler = CreateChromaDbHandler(_random.NextString());
        var entries = TestTextsAndEmbeddings.Select(_ => CreateTestEntry()).ToArray();
        const int nResults = 10;
        foreach (var ((text, _), entry) in TestTextsAndEmbeddings.Zip(entries))
        {
            await handler.UpsertEntryAsync(entry, text, CancellationToken.None);
        }
        var testReference = entries.First().Reference;
        var expectedReferences = entries.Select(entry => entry.Reference)
            .Where(reference => reference != testReference).ToList();

        var actual = await handler.GetReferenceAndScoreOfSimilarEntriesAsync(testReference, nResults, [], CancellationToken.None);

        Assert.True(actual.Count < nResults);
        Assert.Equal(entries.Length - 1, actual.Count);
        Assert.Equivalent(expectedReferences, actual.Select(doc => doc.Reference));
    }

    [Fact]
    public async Task GetReferenceAndScoreOfSimilarEntriesAsync_EnoughEntriesInDb_ReturnsOnlyNEntries()
    {
        var handler = CreateChromaDbHandler(_random.NextString());
        var entries = TestTextsAndEmbeddings.Select(_ => CreateTestEntry()).ToArray();
        const int nResults = 1;
        foreach (var ((text, _), entry) in TestTextsAndEmbeddings.Zip(entries))
        {
            await handler.UpsertEntryAsync(entry, text, CancellationToken.None);
        }

        var actual = await handler.GetReferenceAndScoreOfSimilarEntriesAsync(entries.First().Reference,
            nResults, [], CancellationToken.None);

        Assert.Single(actual);
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
            await handler.UpsertEntryAsync(entry, text, CancellationToken.None);
        }
        var testReference = entries.First().Reference;
        var expectedReferences = entries.Where(entry => entry.FeedId == enabledFeedId).Select(entry => entry.Reference)
            .Where(reference => reference != testReference);

        var actual =
            await handler.GetReferenceAndScoreOfSimilarEntriesAsync(testReference, 5, [disabledFeedId],
                CancellationToken.None);

        Assert.Single(actual);
        Assert.Equivalent(expectedReferences, actual.Select(entry => entry.Reference));
    }

    [Fact]
    public async Task GetReferenceAndScoreOfSimilarEntriesAsync_DisabledEntriesInDb_OnlyEnabledEntries()
    {
        var handler = CreateChromaDbHandler(_random.NextString());
        var entries = TestTextsAndEmbeddings.Select(_ => CreateTestEntry()).ToArray();
        foreach (var ((text, _), entry) in TestTextsAndEmbeddings.Zip(entries))
        {
            await handler.UpsertEntryAsync(entry, text, CancellationToken.None);
        }
        var disabledGist = new Gist(entries.Last(), TestSummaryAIResponse);
        await handler.EnsureGistHasCorrectMetadataAsync(disabledGist, true, CancellationToken.None);
        var testReference = entries.First().Reference;
        var expectedReferences = entries.SkipLast(1).Select(entry => entry.Reference)
            .Where(reference => reference != testReference);

        var actual =
            await handler.GetReferenceAndScoreOfSimilarEntriesAsync(testReference, 5, [],
                CancellationToken.None);

        Assert.Single(actual);
        Assert.Equivalent(expectedReferences,  actual.Select(entry => entry.Reference));
    }

    private ChromaDbHandler CreateChromaDbHandler(string? collectionName = null)
    {
        var options = collectionName is null
            ? _handlerOptions
            : _handlerOptions with { GistsCollectionName = collectionName };
        return new ChromaDbHandler(CreateOpenAIHandlerMock(), new HttpClient(), Options.Create(options), null);
    }
}
