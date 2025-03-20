using Google.Apis.Http;
using Polly;
using IHttpClientFactory = System.Net.Http.IHttpClientFactory;

namespace GistBackend.Handler.GoogleSearchHandler;

public class RetryingHttpClientFactory(IHttpClientFactory httpClientFactory) : HttpClientFactory
{
    public HttpClient CreateClient(string name)
    {
        var client = httpClientFactory.CreateClient(name);
        var retryPolicy = Policy.HandleResult<HttpResponseMessage>(r => (int)r.StatusCode >= 500)
                                .WaitAndRetryAsync(3, retryAttempt =>
                                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
        client.DefaultRequestHeaders.Add("Retry-After", retryPolicy.ToString());
        return client;
    }
}

