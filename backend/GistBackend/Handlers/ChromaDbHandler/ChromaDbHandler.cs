using System.Net;
using System.Text;
using System.Text.Json;
using GistBackend.Exceptions;
using GistBackend.Handlers.AIHandler;
using GistBackend.Types;
using GistBackend.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static GistBackend.Utils.LogEvents;

namespace GistBackend.Handlers.ChromaDbHandler;

public interface IChromaDbHandler
{
    Task UpsertEntryAsync(RssEntry entry, string summary, CancellationToken ct);
    Task<bool> EnsureGistHasCorrectMetadataAsync(Gist gist, bool disabled, CancellationToken ct);
    Task<List<SimilarDocument>> GetReferenceAndScoreOfSimilarEntriesAsync(
        string reference, int nResults, IEnumerable<int> disabledFeedIds, CancellationToken ct);
}

public class ChromaDbHandler : IChromaDbHandler
{
    private readonly Uri _chromaDbUri;
    private readonly string _tenantName;
    private readonly string _databaseName;
    private readonly string _collectionName;
    private readonly IAIHandler _aiHandler;
    private readonly HttpClient _httpClient;
    private readonly string _credentialsHeaderName;
    private readonly string _serverAuthnCredentials;
    private readonly ILogger<ChromaDbHandler>? _logger;

    public ChromaDbHandler(IAIHandler aiHandler,
        HttpClient httpClient,
        IOptions<ChromaDbHandlerOptions> options,
        ILogger<ChromaDbHandler>? logger)
    {
        if (string.IsNullOrWhiteSpace(options.Value.Server))
            throw new ArgumentException("Server is not set in the options.");
        if (string.IsNullOrWhiteSpace(options.Value.ServerAuthnCredentials))
            throw new ArgumentException("Server authentication credentials are not set in the options.");
        _aiHandler = aiHandler;
        _httpClient = httpClient;
        _logger = logger;
        _chromaDbUri = new Uri($"http://{options.Value.Server}:{options.Value.Port}/");
        _credentialsHeaderName = options.Value.CredentialsHeaderName;
        _serverAuthnCredentials = options.Value.ServerAuthnCredentials;
        _tenantName = options.Value.GistsTenantName;
        _databaseName = options.Value.GistsDatabaseName;
        _collectionName = options.Value.GistsCollectionName;
    }

    private static readonly string[] IncludeOnGet = ["metadatas", "distances"];

    public async Task<List<SimilarDocument>> GetReferenceAndScoreOfSimilarEntriesAsync(string reference,
        int nResults, IEnumerable<int> disabledFeedIds, CancellationToken ct)
    {
        ValidateReference(reference);
        var collectionId = await GetOrCreateCollectionAsync(ct);
        if (!await EntryExistsByReferenceAsync(reference, ct, collectionId))
        {
            throw new DatabaseOperationException("Entry does not exist in database");
        }

        var document = await GetDocumentByReferenceAsync(reference, collectionId, true, false, ct);
        var content = CreateStringContent(new {
            QueryEmbeddings = new[] {document.Embeddings!.Single()},
            NResults = nResults+1, // +1 to exclude the original entry
            Where = GenerateWhere(disabledFeedIds),
            Include = IncludeOnGet
        });
        var response = await SendPostRequestAsync(
            $"/api/v2/tenants/{_tenantName}/databases/{_databaseName}/collections/{collectionId}/query", content, ct);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw await CreateDatabaseOperationExceptionAsync("Could not query similar entries", response, ct);
        }

        var responseContent = await response.Content.ReadAsStreamAsync(ct);
        var queryResponse =
            await JsonSerializer.DeserializeAsync<QueryResponse>(responseContent, SerializerDefaults.JsonOptions, ct);
        if (queryResponse is null) throw new DatabaseOperationException("Could not get similar entries");
        var referencesAndScores = ExtractReferencesAndScores(queryResponse);

        // Exclude the original entry from the results
        return referencesAndScores.Where(referenceAndScore => referenceAndScore.Reference != reference).ToList();
    }

    private static Dictionary<string, object> GenerateWhere(IEnumerable<int> disabledFeedIds)
    {
        var whereNotDisabled = new Dictionary<string, object> {
            { "disabled", new Dictionary<string, object> { { "$ne", true } } }
        };
        var disabledFeedIdsArray = disabledFeedIds.ToArray();
        if (disabledFeedIdsArray.Length == 0)
        {
            return whereNotDisabled;
        }

        var whereNotInDisabledFeeds = new Dictionary<string, object> {
            { "feed_id", new Dictionary<string, object> { { "$nin", disabledFeedIdsArray } } }
        };
        return new Dictionary<string, object> {
            { "$and", new[] {
                whereNotDisabled,
                whereNotInDisabledFeeds
            } }
        };
    }

    private static List<SimilarDocument> ExtractReferencesAndScores(QueryResponse queryResponse) =>
        Enumerable.Range(0, queryResponse.Ids.First().Length).Select(i =>
            new SimilarDocument(
                queryResponse.Metadatas.First()[i].Reference,
                ConvertCosineDistanceToSimilarity(queryResponse.Distances.First()[i])
            ))
            .ToList();

    private static float ConvertCosineDistanceToSimilarity(float distance) => float.Clamp(1 - distance/2, 0, 1);

    public async Task UpsertEntryAsync(RssEntry entry, string summary, CancellationToken ct)
    {
        ValidateReference(entry.Reference);
        var collectionId = await GetOrCreateCollectionAsync(ct);
        var mode = "add";
        if (await EntryExistsByReferenceAsync(entry.Reference, ct, collectionId))
        {
            _logger?.LogInformation(EntryAlreadyExistsInChromaDb,
                "Entry with reference {Reference} already exists in database", entry.Reference);
            mode = "update";
        }

        var metadata = new Metadata(entry.Reference, entry.FeedId);
        var embedding = await _aiHandler.GenerateEmbeddingAsync(summary, ct);
        var content = CreateStringContent(new Document([entry.Reference], [metadata], [embedding]));
        var response = await SendPostRequestAsync(
            $"/api/v2/tenants/{_tenantName}/databases/{_databaseName}/collections/{collectionId}/{mode}", content, ct);

        if (mode == "add" && response.StatusCode != HttpStatusCode.Created ||
            mode == "update" && response.StatusCode != HttpStatusCode.OK)
        {
            throw await CreateDatabaseOperationExceptionAsync($"Could not {mode} entry", response, ct);
        }
        _logger?.LogInformation(DocumentInserted,
            "Upserted ({Mode}) document with metadata {Metadata} for entry with reference {Reference}",
            mode, metadata, entry.Reference);
    }

    public async Task<bool> EnsureGistHasCorrectMetadataAsync(Gist gist, bool disabled, CancellationToken ct)
    {
        ValidateReference(gist.Reference);
        var collectionId = await GetOrCreateCollectionAsync(ct);
        var document = await GetDocumentByReferenceAsync(gist.Reference, collectionId, false, true, ct);
        var oldMetadata = document.Metadatas.FirstOrDefault();
        if (oldMetadata is null)
        {
            throw new DatabaseOperationException($"Entry with reference {gist.Reference} does not exist in ChromaDb");
        }
        if (oldMetadata.Disabled == disabled && oldMetadata.FeedId == gist.FeedId) return true;
        var newMetaData = new Metadata(gist.Reference, gist.FeedId, disabled);
        await UpdateMetadataAsync(gist.Reference, newMetaData, ct);
        _logger?.LogInformation(ChangedMetadataOfGistInChromaDb,
            "Changed metadata from {OldMetadata} to {NewMetadata} for gist with reference {GistReference}",
            oldMetadata, newMetaData, gist.Reference);
        return false;
    }

    private async Task UpdateMetadataAsync(string reference, Metadata metadata, CancellationToken ct)
    {
        ValidateReference(reference);
        var collectionId = await GetOrCreateCollectionAsync(ct);
        if (!await EntryExistsByReferenceAsync(reference, ct, collectionId))
        {
            throw new DatabaseOperationException("Entry to update does not exist");
        }
        var content = CreateStringContent(new Document([reference], [metadata]));
        var response = await SendPostRequestAsync(
            $"/api/v2/tenants/{_tenantName}/databases/{_databaseName}/collections/{collectionId}/update", content, ct);

        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw await CreateDatabaseOperationExceptionAsync("Could not update entry", response, ct);
        }
    }

    public async Task<bool> EntryExistsByReferenceAsync(string reference, CancellationToken ct, string? collectionId = null)
    {
        collectionId ??= await GetOrCreateCollectionAsync(ct);
        var document = await GetDocumentByReferenceAsync(reference, collectionId, false, false, ct);
        return document.Ids.Length != 0;
    }

    private async Task<Document> GetDocumentByReferenceAsync(string reference, string collectionId,
        bool includeEmbeddings, bool includeMetadata, CancellationToken ct)
    {
        var include = new List<string>();
        if (includeEmbeddings) include.Add("embeddings");
        if (includeMetadata) include.Add("metadatas");
        var content = CreateStringContent(new { Ids = new[] { reference }, Include = include });
        var response = await SendPostRequestAsync(
            $"/api/v2/tenants/{_tenantName}/databases/{_databaseName}/collections/{collectionId}/get", content, ct);
        var responseContent = await response.Content.ReadAsStreamAsync(ct);
        var document =
            await JsonSerializer.DeserializeAsync<Document>(responseContent, SerializerDefaults.JsonOptions, ct);
        if (document is null || (includeEmbeddings && document.Embeddings is null))
        {
            throw await CreateDatabaseOperationExceptionAsync("Could not get entry", response, ct);
        }
        return document;
    }

    private async Task<string> GetOrCreateCollectionAsync(CancellationToken ct)
    {
        await CreateDatabaseIfNotExistsAsync(ct);
        var existingCollectionId = await GetCollectionIdAsync(_collectionName, ct);
        if (existingCollectionId is not null) return existingCollectionId;

        var requestContent = CreateStringContent(new CollectionDefinition(_collectionName));
        var response =
            await SendPostRequestAsync($"/api/v2/tenants/{_tenantName}/databases/{_databaseName}/collections",
                requestContent, ct);

        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw await CreateDatabaseOperationExceptionAsync("Could not create collection", response, ct);
        }
        return await ExtractCollectionIdAsync(response, ct);
    }

    private async Task<string?> GetCollectionIdAsync(string collectionName, CancellationToken ct)
    {
        var response = await SendGetRequestAsync(
            $"api/v2/tenants/{_tenantName}/databases/{_databaseName}/collections/{collectionName}", ct);
        return response.StatusCode == HttpStatusCode.NotFound ? null : await ExtractCollectionIdAsync(response, ct);
    }

    private static async Task<string> ExtractCollectionIdAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var content = await response.Content.ReadAsStreamAsync(ct);
        var collection =
            await JsonSerializer.DeserializeAsync<Collection>(content, SerializerDefaults.JsonOptions, ct);
        if (collection is null)
        {
            throw await CreateDatabaseOperationExceptionAsync("Could not extract collection ID", response, ct);
        }
        return collection.Id;
    }

    private async Task CreateDatabaseIfNotExistsAsync(CancellationToken ct)
    {
        await CreateTenantIfNotExistsAsync(ct);
        if (await DatabaseExistsAsync(ct)) return;
        var content = CreateStringContent(new { Name = _databaseName });
        var response = await SendPostRequestAsync($"/api/v2/tenants/{_tenantName}/databases", content, ct);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw await CreateDatabaseOperationExceptionAsync("Could not create database", response, ct);
        }
    }

    private async Task<bool> DatabaseExistsAsync(CancellationToken ct)
    {
        var response = await SendGetRequestAsync($"api/v2/tenants/{_tenantName}/databases/{_databaseName}", ct);
        return response.StatusCode == HttpStatusCode.OK;
    }

    private async Task CreateTenantIfNotExistsAsync(CancellationToken ct)
    {
        if (await TenantExistsAsync(ct)) return;
        var content = CreateStringContent(new { Name = _tenantName });
        var response = await SendPostRequestAsync("/api/v2/tenants", content, ct);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw await CreateDatabaseOperationExceptionAsync("Could not create tenant", response, ct);
        }
    }

    private async Task<bool> TenantExistsAsync(CancellationToken ct)
    {
        var response = await SendGetRequestAsync($"api/v2/tenants/{_tenantName}", ct);
        return response.StatusCode == HttpStatusCode.OK;
    }

    private Task<HttpResponseMessage> SendGetRequestAsync(string relativeUri, CancellationToken ct) =>
        SendRequestAsync(HttpMethod.Get, relativeUri, ct);

    private Task<HttpResponseMessage> SendPostRequestAsync(string relativeUri, HttpContent content,
        CancellationToken ct) => SendRequestAsync(HttpMethod.Post, relativeUri, ct, content);

    private async Task<HttpResponseMessage> SendRequestAsync(HttpMethod method, string relativeUri,
        CancellationToken ct, HttpContent? content = null)
    {
        var uri = new Uri(_chromaDbUri, relativeUri);
        var request = CreateHttpRequestMessage(method, uri, content);
        return await _httpClient.SendAsync(request, ct);
    }

    private static StringContent CreateStringContent(object objectToSerialize) =>
        new(JsonSerializer.Serialize(objectToSerialize, SerializerDefaults.JsonOptions), Encoding.UTF8,
            "application/json");

    private HttpRequestMessage CreateHttpRequestMessage(HttpMethod method, Uri uri, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Add(_credentialsHeaderName, _serverAuthnCredentials);
        request.Content = content;
        return request;
    }

    private static async Task<DatabaseOperationException> CreateDatabaseOperationExceptionAsync(string message,
        HttpResponseMessage response, CancellationToken ct)
    {
        var responseContent = await response.Content.ReadAsStringAsync(ct);
        return new DatabaseOperationException($"{message}. Code: {response.StatusCode}. Response: {responseContent}");
    }

    private static void ValidateReference(string reference)
    {
        if (reference.Length is 0 or >= 1000000) throw new ArgumentException("Reference is invalid.");
    }
}
