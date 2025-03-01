using GistBackend.Types;

namespace GistBackend.Handler;

public interface IChromaDbHandler {
    public Task UpsertEntryAsync(RssEntry entry, CancellationToken ct);
}

public class ChromaDbHandler : IChromaDbHandler {
    public Task UpsertEntryAsync(RssEntry entry, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}
