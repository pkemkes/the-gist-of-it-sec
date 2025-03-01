using GistBackend.Types;

namespace GistBackend.Handler;

public interface IGoogleSearchHandler {
    public Task<IEnumerable<GoogleSearchResult>> GetSearchResultsAsync(string searchQuery, CancellationToken ct);
}

public class GoogleSearchHandler : IGoogleSearchHandler {
    public Task<IEnumerable<GoogleSearchResult>> GetSearchResultsAsync(string searchQuery, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}
