using GistBackend.Handler;
using GistBackend.Handler.MariaDbHandler;
using GistBackend.Handler.OpenAiHandler;
using GistBackend.Utils;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GistBackend.Services;

public class RecapService(
    IMariaDbHandler mariaDbHandler,
    IOpenAIHandler openAIHandler,
    IDateTimeHandler dateTimeHandler,
    ILogger<RecapService>? logger = null) : BackgroundService
{
    private const int UtcHourToCreateRecapAt = 5;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var startTime = dateTimeHandler.GetUtcNow();
            if (await DailyRecapIsNecessaryAsync(startTime, ct))
            {
                await CreateDailyRecapAsync(ct);
            }
            if (await WeeklyRecapIsNecessaryAsync(startTime, ct))
            {
                await CreateWeeklyRecapAsync(ct);
            }
            await ServiceUtils.DelayUntilNextExecutionAsync(startTime, logger, ct, dateTimeHandler);
        }
    }

    private async Task<bool> DailyRecapIsNecessaryAsync(DateTimeOffset now, CancellationToken ct) =>
        now.Hour == UtcHourToCreateRecapAt && !await mariaDbHandler.DailyRecapExistsAsync(ct);

    private async Task<bool> WeeklyRecapIsNecessaryAsync(DateTimeOffset now, CancellationToken ct) =>
        now.Hour == UtcHourToCreateRecapAt && !await mariaDbHandler.WeeklyRecapExistsAsync(ct);

    private async Task CreateDailyRecapAsync(CancellationToken ct)
    {
        var gists = await mariaDbHandler.GetGistsOfLastDayAsync(ct);
        if (gists.Count == 0)
        {
            logger?.LogInformation("No gists to create daily recap");
            return;
        }
        var recap = await openAIHandler.GenerateDailyRecapAsync(gists, ct);
        await mariaDbHandler.InsertDailyRecapAsync(recap, ct);
    }

    private async Task CreateWeeklyRecapAsync(CancellationToken ct)
    {
        var gists = await mariaDbHandler.GetGistsOfLastWeekAsync(ct);
        if (gists.Count == 0)
        {
            logger?.LogInformation("No gists to create weekly recap");
            return;
        }
        var recap = await openAIHandler.GenerateWeeklyRecapAsync(gists, ct);
        await mariaDbHandler.InsertWeeklyRecapAsync(recap, ct);
    }
}
