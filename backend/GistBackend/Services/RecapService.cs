using GistBackend.Handlers;
using GistBackend.Handlers.MariaDbHandler;
using GistBackend.Handlers.OpenAiHandler;
using GistBackend.Utils;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Prometheus;
using static GistBackend.Utils.LogEvents;

namespace GistBackend.Services;

public class RecapService(
    IMariaDbHandler mariaDbHandler,
    IOpenAIHandler openAIHandler,
    IDateTimeHandler dateTimeHandler,
    ILogger<RecapService>? logger = null) : BackgroundService
{
    private static readonly Gauge DailyRecapGauge =
        Metrics.CreateGauge("daily_recap_seconds", "Time spent to create daily recap");
    private static readonly Gauge WeeklyRecapGauge =
        Metrics.CreateGauge("weekly_recap_seconds", "Time spent to create weekly recap");
    private const int UtcHourToCreateRecapAt = 5;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var startTime = dateTimeHandler.GetUtcNow();
            await CreateDailyRecapIfNecessaryAsync(startTime, ct);
            await CreateWeeklyRecapIfNecessaryAsync(startTime, ct);
            await ServiceUtils.DelayUntilNextExecutionAsync(startTime, 5, logger, ct, dateTimeHandler);
        }
    }

    private async Task CreateDailyRecapIfNecessaryAsync(DateTime startTime, CancellationToken ct)
    {
        if (await DailyRecapIsNecessaryAsync(startTime, ct))
        {
            using (new SelfReportingStopwatch(elapsed => DailyRecapGauge.Set(elapsed)))
            {
                await CreateDailyRecapAsync(ct);
            }
        }
    }

    private async Task CreateWeeklyRecapIfNecessaryAsync(DateTime startTime, CancellationToken ct)
    {
        if (await WeeklyRecapIsNecessaryAsync(startTime, ct))
        {
            using (new SelfReportingStopwatch(elapsed => WeeklyRecapGauge.Set(elapsed)))
            {
                await CreateWeeklyRecapAsync(ct);
            }
        }
    }

    private async Task<bool> DailyRecapIsNecessaryAsync(DateTimeOffset now, CancellationToken ct) =>
        now.Hour >= UtcHourToCreateRecapAt && !await mariaDbHandler.DailyRecapExistsAsync(ct);

    private async Task<bool> WeeklyRecapIsNecessaryAsync(DateTimeOffset now, CancellationToken ct) =>
        now.Hour >= UtcHourToCreateRecapAt && !await mariaDbHandler.WeeklyRecapExistsAsync(ct);

    private async Task CreateDailyRecapAsync(CancellationToken ct)
    {
        var gists = await mariaDbHandler.GetGistsOfLastDayAsync(ct);
        if (gists.Count == 0)
        {
            logger?.LogInformation(NoGistsForDailyRecap, "No gists to create daily recap");
            return;
        }
        var recap = await openAIHandler.GenerateDailyRecapAsync(gists, ct);
        await mariaDbHandler.InsertDailyRecapAsync(recap, ct);
        logger?.LogInformation(DailyRecapCreated, "Daily recap created");
    }

    private async Task CreateWeeklyRecapAsync(CancellationToken ct)
    {
        var gists = await mariaDbHandler.GetGistsOfLastWeekAsync(ct);
        if (gists.Count == 0)
        {
            logger?.LogInformation(NoGistsForWeeklyRecap, "No gists to create weekly recap");
            return;
        }
        var recap = await openAIHandler.GenerateWeeklyRecapAsync(gists, ct);
        await mariaDbHandler.InsertWeeklyRecapAsync(recap, ct);
        logger?.LogInformation(WeeklyRecapCreated, "Weekly recap created");
    }
}
