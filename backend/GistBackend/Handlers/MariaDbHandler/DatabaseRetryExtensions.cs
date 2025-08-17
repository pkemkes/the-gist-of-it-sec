using Microsoft.Extensions.Logging;
using MySqlConnector;
using Polly;
using static GistBackend.Utils.LogEvents;

namespace GistBackend.Handlers.MariaDbHandler;

public static class DatabaseRetryExtensions
{
    public static async Task<T> WithDeadlockRetry<T>(this Task<T> task, ILogger? logger = null)
    {
        return await CreateDeadlockRetryPolicy(logger).ExecuteAsync(() => task);
    }

    public static async Task WithDeadlockRetry(this Task task, ILogger? logger = null)
    {
        await CreateDeadlockRetryPolicy(logger).ExecuteAsync(() => task);
    }

    private static Polly.Retry.AsyncRetryPolicy CreateDeadlockRetryPolicy(ILogger? logger) =>
        Policy
            .Handle<MySqlException>(ex => ex.Number == 1213) // 1213 is deadlock error code
            .WaitAndRetryAsync(
                5,
                attempt => TimeSpan.FromMilliseconds(100 * Math.Pow(2, attempt-1)),
                (_, _, attempt, _) =>
                    logger?.LogWarning(DatabaseOperationRetry, "Deadlock detected, retry attempt {Attempt}/5", attempt)
            );
}
