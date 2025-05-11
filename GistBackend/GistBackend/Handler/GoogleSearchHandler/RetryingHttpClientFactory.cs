using Google.Apis.Http;
using Polly;
using Polly.Retry;
using static Polly.Policy;
using IHttpClientFactory = System.Net.Http.IHttpClientFactory;

namespace GistBackend.Handler.GoogleSearchHandler;

public class RetryingHttpClientFactory(IHttpClientFactory httpClientFactory) : HttpClientFactory
{
    protected override ConfigurableMessageHandler CreateHandler(CreateHttpClientArgs args)
    {
        // Use the factory to create a client-based handler
        var httpClient = httpClientFactory.CreateClient(args.ApplicationName ?? "GoogleSearchClient");
        var innerHandler = new RetryingMessageHandler(new HttpClientHandler(), httpClient);
        return new ConfigurableMessageHandler(innerHandler);
    }
}

public class RetryingMessageHandler : DelegatingHandler
{
    private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy =
        HandleResult<HttpResponseMessage>(r => (int)r.StatusCode >= 500)
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

    private readonly HttpClient _httpClient;

    public RetryingMessageHandler(HttpMessageHandler innerHandler, HttpClient httpClient)
    {
        InnerHandler = innerHandler;
        _httpClient = httpClient;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken) =>
        await _retryPolicy.ExecuteAsync(() =>
            _httpClient.SendAsync(request, cancellationToken));
}
