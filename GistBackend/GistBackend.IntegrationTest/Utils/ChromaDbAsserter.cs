using System.Net;
using System.Text;
using System.Text.Json;
using GistBackend.Handler.ChromaDbHandler;

namespace GistBackend.IntegrationTest.Utils;

public class ChromaDbAsserter(ChromaDbHandlerOptions options)
{
    private readonly HttpClient _httpClient = new();
    private readonly Uri _chromaDbUri = new($"http://{options.Server}:{options.Port}/");
    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
        { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task AssertEmbeddingsInDbAsync(string reference, float[] expectedEmbeddings)
    {
        var collectionId = await GetCollectionIdAsync();
        var document = await GetDocumentAsync(collectionId, reference);
        Assert.Single(document.Ids);
        Assert.Equal(reference, document.Ids.Single());
        Assert.Single(document.Metadatas);
        Assert.Equal(reference, document.Metadatas.Single().Reference);
        Assert.Single(document.Embeddings);
        Assert.Equivalent(expectedEmbeddings, document.Embeddings.Single());
    }

    private async Task<Document> GetDocumentAsync(string collectionId, string reference)
    {
        var uri = new Uri(_chromaDbUri,
            $"/api/v2/tenants/{options.GistsTenantName}/databases/{options.GistsDatabaseName}/collections/{collectionId}/get");
        var requestBody = new { ids = new[] { reference }, include = new[] { "metadatas", "embeddings" } };
        var requestContent = new StringContent(JsonSerializer.Serialize(requestBody, _jsonSerializerOptions),
            Encoding.UTF8, "application/json");
        var requestMessage = CreateHttpRequestMessage(HttpMethod.Post, uri, requestContent);
        var response = await _httpClient.SendAsync(requestMessage);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var responseContent = await response.Content.ReadAsStreamAsync();
        return (await JsonSerializer.DeserializeAsync<Document>(responseContent, _jsonSerializerOptions))!;
    }

    private async Task<string> GetCollectionIdAsync()
    {
        var uri = new Uri(_chromaDbUri,
            $"/api/v2/tenants/{options.GistsTenantName}/databases/{options.GistsDatabaseName}/collections/{options.GistsCollectionName}");
        var requestMessage = CreateHttpRequestMessage(HttpMethod.Get, uri);
        var response = await _httpClient.SendAsync(requestMessage);
        var responseContent = await response.Content.ReadAsStreamAsync();
        var collection = await JsonSerializer.DeserializeAsync<Collection>(responseContent, _jsonSerializerOptions);
        Assert.NotNull(collection);
        return collection.Id;
    }

    private HttpRequestMessage CreateHttpRequestMessage(HttpMethod method, Uri uri, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Add(options.CredentialsHeaderName, options.ServerAuthnCredentials);
        request.Content = content;
        return request;
    }
}
