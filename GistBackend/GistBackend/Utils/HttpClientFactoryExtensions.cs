namespace GistBackend.Utils;

public static class HttpClientFactoryExtensions
{
    public static HttpClient CreateClientWithRetryAndCustomUserAgent(
        this IHttpClientFactory httpClientFactory,
        string name,
        string userAgent)
    {
        var client = httpClientFactory.CreateClient(name);
        client.DefaultRequestHeaders.Add("User-Agent", userAgent);
        return client;
    }
}
