using GistBackend.Handlers;
using GistBackend.Handlers.MariaDbHandler;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GistBackend.Utils;

public static class ServiceProviderExtensions
{
    public static MariaDbHandler GetKeyedMariaDbHandler(
        this IServiceProvider provider, string optionsName)
    {
        var options = provider.GetRequiredService<IOptionsSnapshot<MariaDbHandlerOptions>>().Get(optionsName);
        return new MariaDbHandler(
            Options.Create(options),
            provider.GetRequiredService<IDateTimeHandler>(),
            provider.GetService<ILogger<MariaDbHandler>>()
        );
    }
}
