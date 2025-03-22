using Google.Apis.CustomSearchAPI.v1;
using Google.Apis.CustomSearchAPI.v1.Data;
using Google.Apis.Services;
using Microsoft.Extensions.Options;

namespace GistBackend.Handler.GoogleSearchHandler;

public record CustomSearchApiHandlerOptions(
    string ApiKey,
    string EngineId
);

public interface ICustomSearchApiHandler
{
    public Task<Search?> ExecuteSearchAsync(string searchQuery, CancellationToken ct);
}

public class CustomSearchApiHandler(
    IOptions<CustomSearchApiHandlerOptions> options,
    IHttpClientFactory httpClientFactory) : ICustomSearchApiHandler
{
    private readonly CustomSearchAPIService _customSearchApiService = new(new BaseClientService.Initializer {
        ApiKey = options.Value.ApiKey,
        HttpClientFactory = new RetryingHttpClientFactory(httpClientFactory)
    });
    private readonly string _engineId = options.Value.EngineId;

    public Task<Search?> ExecuteSearchAsync(string searchQuery, CancellationToken ct)
    {
        var listRequest = _customSearchApiService.Cse.List();
        listRequest.Cx = _engineId;
        listRequest.Q = searchQuery;
        return listRequest.ExecuteAsync(ct);
    }
}
