namespace GistBackend.Utils;

public record DebounceInfo
{
    public int Count { get; set; }
    public int Score { get; set; }
}

public interface IGistDebouncer
{
    bool IsDebounced(int gistId);
    void ResetDebounceState(int gistId);
    int GetDebouncedGistsCount();
}

public class GistDebouncer : IGistDebouncer
{
    private readonly Dictionary<int, DebounceInfo> _debouncedGists = new();
    private const int MaxDebounceCount = 7;

    public bool IsDebounced(int gistId)
    {
        if (!_debouncedGists.TryGetValue(gistId, out var debounceInfo))
        {
            ResetDebounceState(gistId);
            return false;
        }

        if (debounceInfo.Score == 0)
        {
            IncreaseCount(gistId, debounceInfo);
            return false;
        }

        DecreaseScore(gistId);
        return true;
    }

    public void ResetDebounceState(int gistId)
    {
        _debouncedGists[gistId] = new DebounceInfo();
    }

    private void IncreaseCount(int gistId, DebounceInfo debounceInfo)
    {
        _debouncedGists[gistId].Count = Math.Min(debounceInfo.Count + 1, MaxDebounceCount);
        _debouncedGists[gistId].Score = (int)Math.Pow(2, debounceInfo.Count) - 1;
    }

    private void DecreaseScore(int gistId)
    {
        _debouncedGists[gistId].Score -= 1;
    }

    public int GetDebouncedGistsCount() => _debouncedGists.Values.Count(debounceInfo => debounceInfo.Score != 0);
}
