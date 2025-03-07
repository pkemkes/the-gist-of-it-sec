using System.Net;
using System.Text;
using System.Text.Json;
using GistBackend.Exceptions;
using GistBackend.Types;
using Microsoft.Extensions.Options;

namespace GistBackend.Handler.ChromaDbHandler;

public record ChromaDbHandlerOptions(
    string Server,
    string ServerAuthnCredentials,
    uint Port = 8000,
    string GistsTenantName = "the_gist_of_it_sec",
    string GistsDatabaseName = "the_gist_of_it_sec",
    string GistsCollectionName = "gist_text_contents",
    string CredentialsHeaderName = "X-Chroma-Token"
);

public interface IChromaDbHandler {
    public Task InsertEntryAsync(RssEntry entry, string entryText, CancellationToken ct);
}

public class ChromaDbHandler(
    IOpenAIHandler openAIHandler,
    HttpClient httpClient,
    IOptions<ChromaDbHandlerOptions> options) : IChromaDbHandler
{
    private readonly Uri _chromaDbUri = new($"http://{options.Value.Server}:{options.Value.Port}/");
    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
        { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task InsertEntryAsync(RssEntry entry, string entryText, CancellationToken ct)
    {
        var collectionId = await GetOrCreateCollectionAsync(ct);

        // var content = CreateStringContent(new {
        //     ids = new[] { entry.Reference },
        //     metadatas = new Metadata[] { new(entry.Reference, entry.FeedId) },
        //     embeddings = new[] { await openAIHandler.GenerateEmbeddingsAsync(entryText, ct) }
        // });
        var content = CreateStringContent(new Document(
            [entry.Reference],
            [await openAIHandler.GenerateEmbeddingsAsync(entryText, ct)],
            [new Metadata(entry.Reference, entry.FeedId)]
        ));
        var response = await SendPostRequestAsync(
            $"/api/v2/tenants/{options.Value.GistsTenantName}/databases/{options.Value.GistsDatabaseName}/collections/{collectionId}/add",
            content, ct);

        if (response.StatusCode != HttpStatusCode.Created)
        {
            throw await CreateDatabaseOperationExceptionAsync("Could not add document", response, ct);
        }
    }

    private async Task<string> GetOrCreateCollectionAsync(CancellationToken ct)
    {
        await CreateDatabaseIfNotExistsAsync(ct);
        var existingCollectionId = await GetCollectionIdAsync(options.Value.GistsCollectionName, ct);
        if (existingCollectionId is not null) return existingCollectionId;

        var requestContent = CreateStringContent(new { name = options.Value.GistsCollectionName });
        var response = await SendPostRequestAsync(
            $"/api/v2/tenants/{options.Value.GistsTenantName}/databases/{options.Value.GistsDatabaseName}/collections",
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
            $"api/v2/tenants/{options.Value.GistsTenantName}/databases/{options.Value.GistsDatabaseName}/collections/{collectionName}", ct);
        return response.StatusCode == HttpStatusCode.NotFound ? null : await ExtractCollectionIdAsync(response, ct);
    }

    private async Task<string> ExtractCollectionIdAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var content = await response.Content.ReadAsStreamAsync(ct);
        var collection =
            await JsonSerializer.DeserializeAsync<Collection>(content, _jsonSerializerOptions, ct);
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
        var content = CreateStringContent(new { name = options.Value.GistsDatabaseName });
        var response = await SendPostRequestAsync($"/api/v2/tenants/{options.Value.GistsTenantName}/databases",
            content, ct);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw await CreateDatabaseOperationExceptionAsync("Could not create database", response, ct);
        }
    }

    private async Task<bool> DatabaseExistsAsync(CancellationToken ct)
    {
        var response = await SendGetRequestAsync(
            $"api/v2/tenants/{options.Value.GistsTenantName}/databases/{options.Value.GistsDatabaseName}", ct);
        return response.StatusCode == HttpStatusCode.OK;
    }

    private async Task CreateTenantIfNotExistsAsync(CancellationToken ct)
    {
        if (await TenantExistsAsync(ct)) return;
        var content = CreateStringContent(new { name = options.Value.GistsTenantName });
        var response = await SendPostRequestAsync("/api/v2/tenants", content, ct);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw await CreateDatabaseOperationExceptionAsync("Could not create tenant", response, ct);
        }
    }

    private async Task<bool> TenantExistsAsync(CancellationToken ct)
    {
        var response = await SendGetRequestAsync($"api/v2/tenants/{options.Value.GistsTenantName}", ct);
        return response.StatusCode == HttpStatusCode.OK;
    }

    private Task<HttpResponseMessage> SendGetRequestAsync(string relativeUri, CancellationToken ct) =>
        SendRequestAsync(HttpMethod.Post, relativeUri, ct);

    private Task<HttpResponseMessage> SendPostRequestAsync(string relativeUri, HttpContent content,
        CancellationToken ct) => SendRequestAsync(HttpMethod.Post, relativeUri, ct, content);

    private async Task<HttpResponseMessage> SendRequestAsync(HttpMethod method, string relativeUri,
        CancellationToken ct, HttpContent? content = null)
    {
        var uri = new Uri(_chromaDbUri, relativeUri);
        var request = CreateHttpRequestMessage(method, uri, content);
        return await httpClient.SendAsync(request, ct);
    }

    private StringContent CreateStringContent(object objectToSerialize) =>
        new(JsonSerializer.Serialize(objectToSerialize, _jsonSerializerOptions), Encoding.UTF8, "application/json");

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
}
