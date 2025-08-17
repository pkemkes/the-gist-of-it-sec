using System.Net;
using System.Text;
using System.Text.Json;
using GistBackend.Handlers.ChromaDbHandler;
using GistBackend.Utils;

namespace GistBackend.IntegrationTest.Utils;

public class ChromaDbAsserter(ChromaDbHandlerOptions options)
{
    private readonly HttpClient _httpClient = new();
    private readonly Uri _chromaDbUri = new($"http://{options.Server}:{options.Port}/");

    public async Task AssertDocumentInDbAsync(Document expectedDocument)
    {
        var collectionId = await GetCollectionIdAsync();
        var actualDocument = await GetDocumentAsync(collectionId, expectedDocument.Ids.Single());
        Assert.Equivalent(expectedDocument, actualDocument);
    }

    private async Task<Document> GetDocumentAsync(string collectionId, string reference)
    {
        var uri = new Uri(_chromaDbUri,
            $"/api/v2/tenants/{options.GistsTenantName}/databases/{options.GistsDatabaseName}/collections/{collectionId}/get");
        var requestBody = new { ids = new[] { reference }, include = new[] { "metadatas", "embeddings" } };
        var requestContent = new StringContent(JsonSerializer.Serialize(requestBody, SerializerDefaults.JsonOptions),
            Encoding.UTF8, "application/json");
        var requestMessage = CreateHttpRequestMessage(HttpMethod.Post, uri, requestContent);
        var response = await _httpClient.SendAsync(requestMessage);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var responseContent = await response.Content.ReadAsStreamAsync();
        return (await JsonSerializer.DeserializeAsync<Document>(responseContent, SerializerDefaults.JsonOptions))!;
    }

    private async Task<string> GetCollectionIdAsync()
    {
        var uri = new Uri(_chromaDbUri,
            $"/api/v2/tenants/{options.GistsTenantName}/databases/{options.GistsDatabaseName}/collections/{options.GistsCollectionName}");
        var requestMessage = CreateHttpRequestMessage(HttpMethod.Get, uri);
        var response = await _httpClient.SendAsync(requestMessage);
        var responseContent = await response.Content.ReadAsStreamAsync();
        var collection =
            await JsonSerializer.DeserializeAsync<Collection>(responseContent, SerializerDefaults.JsonOptions);
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
