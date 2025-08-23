using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using static System.GC;
using static GistBackend.Utils.LogEvents;

namespace GistBackend.Handlers;

public interface IWebCrawlHandler
{
    Task<string> FetchPageContentAsync(string url);
    Task<IResponse?> FetchResponseAsync(string url);
}

public class WebCrawlHandler(ILogger<WebCrawlHandler>? logger = null) : IWebCrawlHandler, IAsyncDisposable
{
    private IPlaywright? _playwright;
    private IBrowserContext? _context;
    private IBrowser? _browser;
    private readonly SemaphoreSlim _crawlSemaphore = new(1, 1);
    private const int MaxCrawls = 10;
    private int _crawlCount;

    private readonly List<string> _browserArgs =
    [
        "--no-sandbox",
        "--disable-dev-shm-usage",
        "--disable-blink-features=AutomationControlled",
        "--disable-background-networking",
        "--disable-sync",
        "--disable-extensions",
        "--disable-default-apps",
        "--disable-component-update",
        "--disable-popup-blocking",
        "--no-first-run"
    ];

    private const string ScriptToRemoveWebdriver = """
        Object.defineProperty(navigator, 'webdriver', {
            get: () => undefined,
        });
        Object.defineProperty(navigator, 'plugins', {
            get: () => [1, 2, 3, 4, 5],
        });
        Object.defineProperty(navigator, 'languages', {
            get: () => ['en-US', 'en'],
        });
        const originalQuery = window.navigator.permissions.query;
        window.navigator.permissions.query = (parameters) => (
            parameters.name === 'notifications' ?
                Promise.resolve({ state: Notification.permission }) :
                originalQuery(parameters)
        );
    """;

    private readonly Dictionary<string, string> _httpHeaders = new() {
        ["User-Agent"] =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
        ["Accept"] = "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8",
        ["Accept-Language"] = "en-US,en;q=0.9",
        // ["Accept-Encoding"] = "gzip, deflate, br",  // Let Playwright handle encoding
        ["DNT"] = "1",
        ["Connection"] = "keep-alive",
        ["Upgrade-Insecure-Requests"] = "1",
        ["Sec-Fetch-Dest"] = "document",
        ["Sec-Fetch-Mode"] = "navigate",
        ["Sec-Fetch-Site"] = "none",
        ["Sec-Fetch-User"] = "?1",
        ["Cache-Control"] = "max-age=0"
    };

    public async Task<string> FetchPageContentAsync(string url)
    {
        using (logger?.BeginScope("Fetching page content for URL: {Url}", url))
        {
            try
            {
                await WaitForSemaphoreAsync(nameof(FetchPageContentAsync));
                var (page, _) = await FetchPageAndResponseWithTimeoutAsync(url);
                try
                {
                    return await page.ContentAsync();
                }
                finally
                {
                    await CleanupPageAsync(page);
                }
            }
            finally
            {
                _crawlSemaphore.Release();
                logger?.LogInformation(WebCrawlSemaphoreReleased,
                    "Released crawl semaphore for FetchPageContentAsync");
            }
        }
    }

    public async Task<IResponse?> FetchResponseAsync(string url)
    {
        using (logger?.BeginScope("Fetching response for URL: {Url}", url))
        {
            try
            {
                await WaitForSemaphoreAsync(nameof(FetchResponseAsync));
                var (page, response) = await FetchPageAndResponseWithTimeoutAsync(url);
                try
                {
                    return response;
                }
                finally
                {
                    await CleanupPageAsync(page);
                }
            }
            finally
            {
                _crawlSemaphore.Release();
                logger?.LogInformation(WebCrawlSemaphoreReleased,
                    "Released crawl semaphore for FetchResponseAsync");
            }
        }
    }

    private async Task WaitForSemaphoreAsync(string operationName)
    {
        logger?.LogInformation(WebCrawlSemaphoreWait, "Waiting for crawl semaphore for {OperationName}", operationName);
        await _crawlSemaphore.WaitAsync();
        logger?.LogInformation(WebCrawlSemaphoreAcquired, "Acquired crawl semaphore for {OperationName}", operationName);
    }

    private async Task<(IPage, IResponse?)> FetchPageAndResponseWithTimeoutAsync(string url, int timeoutSeconds = 120, int maxAttempts = 5)
    {
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            if (attempt >= 4)
            {
                logger?.LogInformation(WebCrawlRestartingBrowser, "Restarting browser before attempt {Attempt}",
                    attempt);
                await RestartBrowserAsync();
            }
            var fetchTask = FetchPageAndResponseAsync(url);
            try
            {
                var completedTask = await Task.WhenAny(fetchTask, Task.Delay(TimeSpan.FromSeconds(timeoutSeconds)));
                if (completedTask == fetchTask)
                {
                    // Success within timeout
                    return await fetchTask;
                }

                logger?.LogWarning(WebCrawlFailed, "Timeout ({Timeout}s) reached for attempt {Attempt}", timeoutSeconds,
                    attempt);
            }
            catch (Exception e)
            {
                logger?.LogWarning(WebCrawlFailed, e, "Attempt failed with exception for attempt {Attempt}", attempt);
            }

            await RestartBrowserAsync();
            _crawlCount = 0;
            await Task.Delay(1000);
        }
        throw new TimeoutException($"Failed to fetch page within {timeoutSeconds}s after {maxAttempts} attempts");
    }

    private async Task<(IPage, IResponse?)> FetchPageAndResponseAsync(string url)
    {
        if (_crawlCount >= MaxCrawls)
        {
            logger?.LogInformation(WebCrawlMaxCrawlsReached, "Maximum crawls reached. Restarting browser...");
            await RestartBrowserAsync();
        }
        logger?.LogInformation(WebCrawlStarted, "Starting crawl request");
        await EnsureBrowserInitializedAsync();

        var page = await _context!.NewPageAsync();

        await page.SetViewportSizeAsync(1920, 1080);
        await page.SetExtraHTTPHeadersAsync(_httpHeaders);

        var response = await page.GotoAsync(url, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = 60000
        });

        await WaitForCloudflareBypassAsync(page);

        _crawlCount++;

        return (page, response);
    }

    private async Task EnsureBrowserInitializedAsync()
    {
        if (_browser == null || _context == null || !await IsBrowserHealthyAsync())
        {
            await RestartBrowserAsync();
        }
    }

    private async Task RestartBrowserAsync()
    {
        await DisposeCoreAsync();

        _playwright = await Playwright.CreateAsync();
        _context = await _playwright.Chromium.LaunchPersistentContextAsync("/tmp/playwright-profile",
            new BrowserTypeLaunchPersistentContextOptions
        {
            Headless = true,
            Args = _browserArgs,
            ChromiumSandbox = false
        });

        _browser = _context.Browser;

        await _context.AddInitScriptAsync(ScriptToRemoveWebdriver);
        logger?.LogInformation("Created new persistent browser context");
        _crawlCount = 0;
    }

    private async Task<bool> IsBrowserHealthyAsync()
    {
        if (_browser == null) return false;
        try
        {
            var page = await _browser.NewPageAsync();
            await page.CloseAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task CleanupPageAsync(IPage page)
    {
        try
        {
            await page.CloseAsync();
            await _context!.ClearCookiesAsync();
            await _context.ClearPermissionsAsync();
            Collect();
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to cleanup page");
        }
    }

    private static async Task WaitForCloudflareBypassAsync(IPage page)
    {
        var challengeSelectors = new[]
        {
            "[data-testid='cf-please-wait']",
            ".cf-browser-verification",
            "#cf-please-wait",
            ".cf-checking-browser"
        };

        foreach (var selector in challengeSelectors)
        {
            var element = await page.QuerySelectorAsync(selector);
            if (element == null) continue;
            await page.WaitForTimeoutAsync(5000);
            break;
        }

        await SimulateHumanBehaviorAsync(page);
    }

    private static async Task SimulateHumanBehaviorAsync(IPage page)
    {
        // Simulate mouse movement
        await page.Mouse.MoveAsync(Random.Shared.Next(100, 800), Random.Shared.Next(100, 600));
        await page.WaitForTimeoutAsync(Random.Shared.Next(500, 1500));

        // Simulate scroll
        await page.EvaluateAsync("window.scrollBy(0, 100)");
        await page.WaitForTimeoutAsync(Random.Shared.Next(500, 1000));
    }

    private async Task DisposeCoreAsync()
    {
        if (_context != null)
        {
            await _context.CloseAsync();
            _context = null;
        }
        if (_browser != null)
        {
            await _browser.CloseAsync();
            _browser = null;
        }
        _playwright?.Dispose();
        _playwright = null;
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeCoreAsync();
        SuppressFinalize(this);
    }
}
