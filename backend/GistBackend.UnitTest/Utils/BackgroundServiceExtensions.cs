using System.Reflection;
using Microsoft.Extensions.Hosting;

namespace GistBackend.UnitTest.Utils;

public static class BackgroundServiceExtensions
{
    public static async Task<Task> StartWaitAndFindExecutingTaskAsync(this BackgroundService service)
    {
        await service.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));
        var executingTask = (Task?)typeof(BackgroundService)
            .GetField("_executeTask", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(service);
        Assert.NotNull(executingTask);
        return executingTask;
    }
}
