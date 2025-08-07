using Google.Apis.CustomSearchAPI.v1;
using Google.Apis.CustomSearchAPI.v1.Data;
using Google.Apis.Services;
using Microsoft.Extensions.Options;

namespace GistBackend.Handlers.GoogleSearchHandler;

public interface ICustomSearchApiHandler
{
    public Task<Search?> ExecuteSearchAsync(string searchQuery, CancellationToken ct);
}

public class CustomSearchApiHandler : ICustomSearchApiHandler
{
    private readonly CustomSearchAPIService _customSearchApiService;
    private readonly string _engineId;

    public CustomSearchApiHandler(IOptions<CustomSearchApiHandlerOptions> options)
    {
        if (string.IsNullOrWhiteSpace(options.Value.ApiKey))
            throw new ArgumentException("API key is not set in the options.");
        if (string.IsNullOrWhiteSpace(options.Value.EngineId))
            throw new ArgumentException("Engine ID is not set in the options.");

        _customSearchApiService = new CustomSearchAPIService(new BaseClientService.Initializer
        {
            ApiKey = options.Value.ApiKey
        });
        _engineId = options.Value.EngineId;
    }

    public Task<Search?> ExecuteSearchAsync(string searchQuery, CancellationToken ct)
    {
        var listRequest = _customSearchApiService.Cse.List();
        listRequest.Cx = _engineId;
        listRequest.Q = searchQuery;
        return listRequest.ExecuteAsync(ct);
    }
}
