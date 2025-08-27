using System.Collections.Concurrent;
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

public class WebRequestTask<TResult>(string url)
{
    public string Url { get; } = url;

    public TaskCompletionSource<TResult> CompletionSource { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}

public class WebCrawlHandler : IWebCrawlHandler, IAsyncDisposable
{
    private readonly ILogger<WebCrawlHandler>? _logger;
    private IPlaywright? _playwright;
    private IBrowserContext? _context;
    private IBrowser? _browser;
    private const int MaxCrawls = 10;
    private int _crawlCount;

    private readonly ConcurrentQueue<WebRequestTask<string>> _highPriorityQueue = new();
    private readonly ConcurrentQueue<WebRequestTask<IResponse?>> _normalPriorityQueue = new();

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
        ["DNT"] = "1",
        ["Connection"] = "keep-alive",
        ["Upgrade-Insecure-Requests"] = "1",
        ["Sec-Fetch-Dest"] = "document",
        ["Sec-Fetch-Mode"] = "navigate",
        ["Sec-Fetch-Site"] = "none",
        ["Sec-Fetch-User"] = "?1",
        ["Cache-Control"] = "max-age=0"
    };

    private readonly CancellationTokenSource _dispatcherCts = new();
    private readonly Task _dispatcherTask;

    public WebCrawlHandler(ILogger<WebCrawlHandler>? logger = null)
    {
        _logger = logger;
        _dispatcherTask = Task.Run(() => DispatcherLoopAsync(_dispatcherCts.Token));
    }

    public async Task<string> FetchPageContentAsync(string url)
    {
        var requestTask = new WebRequestTask<string>(url);
        _highPriorityQueue.Enqueue(requestTask);
        _logger?.LogInformation(WaitingForWebCrawlToComplete,
            "Waiting for web crawl to complete for {Url} with {RequestType}", url, nameof(FetchPageContentAsync));
        return await requestTask.CompletionSource.Task;
    }

    public async Task<IResponse?> FetchResponseAsync(string url)
    {
        using (_logger?.BeginScope("{RequestType} for {Url}", nameof(FetchResponseAsync), url))
        {
            var requestTask = new WebRequestTask<IResponse?>(url);
            _normalPriorityQueue.Enqueue(requestTask);
            _logger?.LogInformation(WaitingForWebCrawlToComplete,
                "Waiting for web crawl to complete for {Url} with {RequestType}", url, nameof(FetchResponseAsync));
            return await requestTask.CompletionSource.Task;
        }
    }

    private async Task DispatcherLoopAsync(CancellationToken cancellationToken)
    {
        _logger?.LogInformation(WebCrawlDispatcherLoopStarted, "Web crawl dispatcher loop started");
        while (!cancellationToken.IsCancellationRequested)
        {
            // Always process high-priority requests first
            if (_highPriorityQueue.TryDequeue(out var highPriorityRequest))
            {
                using (_logger?.BeginScope("Working on crawl with {Priority} priority for {Url}", "high",
                           highPriorityRequest.Url))
                {
                    try
                    {
                        var (page, _) = await FetchPageAndResponseWithTimeoutAsync(highPriorityRequest.Url);
                        var content = await page.ContentAsync();
                        await CleanupPageAsync(page);
                        highPriorityRequest.CompletionSource.SetResult(content);
                    }
                    catch (Exception ex)
                    {
                        highPriorityRequest.CompletionSource.SetException(ex);
                        _logger?.LogError(ex, "Error processing high priority request");
                    }
                    continue;
                }
            }
            // If no high-priority, process normal-priority requests
            if (_normalPriorityQueue.TryDequeue(out var normalPriorityRequest))
            {
                using (_logger?.BeginScope("Working on crawl with {Priority} priority for {Url}", "normal",
                           normalPriorityRequest.Url))
                {
                    try
                    {
                        var (page, response) = await FetchPageAndResponseWithTimeoutAsync(normalPriorityRequest.Url);
                        await CleanupPageAsync(page);
                        normalPriorityRequest.CompletionSource.SetResult(response);
                    }
                    catch (Exception ex)
                    {
                        normalPriorityRequest.CompletionSource.SetException(ex);
                        _logger?.LogError(ex, "Error processing normal priority request");
                    }
                    continue;
                }
            }
            // Wait before next iteration
            await Task.Delay(100, cancellationToken);
        }
    }

    private async Task<(IPage, IResponse?)> FetchPageAndResponseWithTimeoutAsync(string url, int timeoutSeconds = 120, int maxAttempts = 5)
    {
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            IPage? page = null;
            var success = false;
            try
            {
                if (attempt >= 4)
                {
                    _logger?.LogInformation(WebCrawlRestartingBrowser, "Restarting browser before attempt {Attempt}", attempt);
                    await RestartBrowserAsync();
                }
                var result = await TryFetchPageAndResponseAsync(url, timeoutSeconds);
                page = result.Item1;
                success = true;
                return result;
            }
            catch (TimeoutException)
            {
                _logger?.LogWarning(WebCrawlFailed, "Timeout ({Timeout}s) reached for attempt {Attempt}", timeoutSeconds, attempt);
            }
            catch (Exception e)
            {
                _logger?.LogWarning(WebCrawlFailed, e, "Attempt failed with exception for attempt {Attempt}", attempt);
            }
            finally
            {
                // Only cleanup if not successful (i.e., page not returned)
                if (!success && page != null) await CleanupPageAsync(page);
            }
            await RestartBrowserAsync();
            _crawlCount = 0;
            await Task.Delay(1000);
        }
        throw new TimeoutException($"Failed to fetch page within {timeoutSeconds}s after {maxAttempts} attempts");
    }

    private async Task<(IPage, IResponse?)> TryFetchPageAndResponseAsync(string url, int timeoutSeconds)
    {
        var fetchTask = FetchPageAndResponseAsync(url);
        var delayTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));
        var completedTask = await Task.WhenAny(fetchTask, delayTask);
        if (completedTask == fetchTask)
            return await fetchTask;

        // Timeout occurred, but fetchTask may still complete later
        if (fetchTask.IsCompletedSuccessfully || fetchTask.IsCompleted)
        {
            var result = fetchTask.Result;
            var page = result.Item1;
            await CleanupPageAsync(page);
        }
        throw new TimeoutException();
    }

    private async Task<(IPage, IResponse?)> FetchPageAndResponseAsync(string url)
    {
        if (_crawlCount >= MaxCrawls)
        {
           _logger?.LogInformation(WebCrawlMaxCrawlsReached, "Maximum crawls reached. Restarting browser...");
            await RestartBrowserAsync();
        }
        _logger?.LogInformation(WebCrawlStarted, "Starting crawl request");
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

        _logger?.LogInformation(WebCrawlCompleted, "Crawl request completed");
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
        _logger?.LogInformation("Created new persistent browser context");
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
            _logger?.LogWarning(ex, "Failed to cleanup page");
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
        await _dispatcherCts.CancelAsync();
        try
        {
            await _dispatcherTask;
        }
        catch (OperationCanceledException) { }
        await DisposeCoreAsync();
        SuppressFinalize(this);
    }
}
