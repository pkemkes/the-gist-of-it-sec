using System.Net;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Text.Json;
using GistBackend.Exceptions;
using GistBackend.Handler.OpenAiHandler;
using GistBackend.Types;
using GistBackend.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static GistBackend.Utils.LogEvents;

namespace GistBackend.Handler.ChromaDbHandler;

public interface IChromaDbHandler {
    Task InsertEntryAsync(RssEntry entry, string text, CancellationToken ct);
    Task<bool> EnsureGistHasCorrectMetadataAsync(Gist gist, bool disabled, CancellationToken ct);
}

public class ChromaDbHandler(
    IOpenAIHandler openAIHandler,
    HttpClient httpClient,
    IOptions<ChromaDbHandlerOptions> options,
    ILogger<ChromaDbHandler>? logger) : IChromaDbHandler
{
    private readonly Uri _chromaDbUri = new($"http://{options.Value.Server}:{options.Value.Port}/");
    private readonly string _tenantName = options.Value.GistsTenantName;
    private readonly string _databaseName = options.Value.GistsDatabaseName;
    private readonly string _collectionName = options.Value.GistsCollectionName;
    private static readonly string[] IncludeOnGet = ["metadatas", "distances"];

    public async Task<SimilarDocument[]> GetReferenceAndScoreOfSimilarEntriesAsync(string reference, CancellationToken ct,
        int nResults = 6, IEnumerable<int>? disabledFeedIds = null)
    {
        ValidateReference(reference);
        var collectionId = await GetOrCreateCollectionAsync(ct);
        if (!await EntryExistsByReferenceAsync(reference, collectionId, ct))
        {
            throw new DatabaseOperationException("Entry does not exist in database");
        }

        var document = await GetDocumentByReferenceAsync(reference, collectionId, true, false, ct);
        var content = CreateStringContent(new {
            QueryEmbeddings = new[] {document.Embeddings!.Single()},
            NResults = nResults,
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
        return ExtractReferencesAndScores(queryResponse);
    }

    private static Dictionary<string, object> GenerateWhere(IEnumerable<int>? disabledFeedIds)
    {
        var whereNotDisabled = new Dictionary<string, object> {
            { "disabled", new Dictionary<string, object> { { "$ne", true } } }
        };
        if (disabledFeedIds is null)
        {
            return whereNotDisabled;
        }

        var whereNotInDisabledFeeds = new Dictionary<string, object> {
            { "feed_id", new Dictionary<string, object> { { "$nin", disabledFeedIds.ToArray() } } }
        };
        return new Dictionary<string, object> {
            { "$and", new[] {
                whereNotDisabled,
                whereNotInDisabledFeeds
            } }
        };
    }

    private static SimilarDocument[] ExtractReferencesAndScores(QueryResponse queryResponse) =>
        Enumerable.Range(0, queryResponse.Ids.First().Length).Select(i =>
            new SimilarDocument(queryResponse.Metadatas.First()[i].Reference, queryResponse.Distances.First()[i]))
            .ToArray();

    public async Task InsertEntryAsync(RssEntry entry, string text, CancellationToken ct)
    {
        ValidateReference(entry.Reference);
        var collectionId = await GetOrCreateCollectionAsync(ct);
        if (await EntryExistsByReferenceAsync(entry.Reference, collectionId, ct))
        {
            throw new DatabaseOperationException("Entry already exists in database");
        }

        var metadata = new Metadata(entry.Reference, entry.FeedId);
        var embedding = await openAIHandler.GenerateEmbeddingAsync(text, ct);
        var content = CreateStringContent(new Document([entry.Reference], [metadata], [embedding]));
        var response = await SendPostRequestAsync(
            $"/api/v2/tenants/{_tenantName}/databases/{_databaseName}/collections/{collectionId}/add", content, ct);

        if (response.StatusCode != HttpStatusCode.Created)
        {
            throw await CreateDatabaseOperationExceptionAsync("Could not insert entry", response, ct);
        }
        logger?.LogInformation(DocumentInserted,
            "Inserted document with metadata {Metadata} for entry with reference {Reference}",
            metadata, entry.Reference);
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
        logger?.LogInformation(ChangedMetadataOfGistInChromaDb,
            "Changed metadata from {OldMetadata} to {NewMetadata} for gist with reference {GistReference}",
            oldMetadata, newMetaData, gist.Reference);
        return false;
    }

    private async Task UpdateMetadataAsync(string reference, Metadata metadata, CancellationToken ct)
    {
        ValidateReference(reference);
        var collectionId = await GetOrCreateCollectionAsync(ct);
        if (!await EntryExistsByReferenceAsync(reference, collectionId, ct))
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

    private async Task<bool> EntryExistsByReferenceAsync(string reference, string collectionId, CancellationToken ct)
    {
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

        var requestContent = CreateStringContent(new { Name = _collectionName });
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
        return await httpClient.SendAsync(request, ct);
    }

    private static StringContent CreateStringContent(object objectToSerialize) =>
        new(JsonSerializer.Serialize(objectToSerialize, SerializerDefaults.JsonOptions), Encoding.UTF8,
            "application/json");

    private HttpRequestMessage CreateHttpRequestMessage(HttpMethod method, Uri uri, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Add(options.Value.CredentialsHeaderName, options.Value.ServerAuthnCredentials);
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
