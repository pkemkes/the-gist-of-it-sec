using System.Diagnostics;

namespace GistBackend.Utils;

public class SelfReportingStopwatch(Action<double> reportElapsedSeconds) : IDisposable
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _stopwatch.Stop();
        reportElapsedSeconds(_stopwatch.Elapsed.TotalSeconds);
    }
}
