using Microsoft.Extensions.Logging;

namespace GistBackend.Utils;

public static class LogEvents {
    public static readonly EventId GistServiceDelayExceeded = new(100, nameof(GistServiceDelayExceeded));
}
