using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using static System.GC;

namespace GistBackend.Handlers;

public interface IWebCrawlHandler
{
    public Task<string> FetchPageContentAsync(string url);
    public Task<IResponse?> FetchResponseAsync(string url);
}

public class WebCrawlHandler(ILogger<WebCrawlHandler>? logger = null) : IWebCrawlHandler, IAsyncDisposable
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public async Task<string> FetchPageContentAsync(string url)
    {
        var (page, _) = await FetchPageAndResponseAsync(url);
        var textContent = await page.ContentAsync();
        await page.CloseAsync();
        return textContent;
    }

    public async Task<IResponse?> FetchResponseAsync(string url)
    {
        var (_, response) = await FetchPageAndResponseAsync(url);
        return response;
    }

    private async Task<(IPage, IResponse?)> FetchPageAndResponseAsync(string url)
    {
        try
        {
            await EnsureBrowserInitializedAsync();

            var page = await _browser!.NewPageAsync();

            try
            {
                // Set up Cloudflare bypass techniques
                await SetupCloudflareBypassAsync(page);

                // Navigate to the page with extended timeout
                var response = await page.GotoAsync(url, new PageGotoOptions {
                    WaitUntil = WaitUntilState.DOMContentLoaded,
                    Timeout = 60000 // Extended timeout for potential challenges
                });

                // Wait for potential Cloudflare challenge to complete
                await WaitForCloudflareBypassAsync(page);

                // Get the HTML content
                return (page, response);
            }
            catch (Exception)
            {
                await page.CloseAsync();
                throw;
            }
        }
        catch (Exception e)
        {
            logger?.LogError(e, "Failed to fetch text content for entry with URL {EntryUrl}", url);
            throw;
        }
    }

    private async Task EnsureBrowserInitializedAsync()
    {
        if (_playwright == null)
        {
            _playwright = await Playwright.CreateAsync();
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
                Args = [
                    "--no-sandbox",
                    "--disable-dev-shm-usage",
                    "--disable-blink-features=AutomationControlled", // Hide automation
                    "--disable-web-security",
                    "--disable-features=VizDisplayCompositor",
                    "--disable-background-timer-throttling",
                    "--disable-backgrounding-occluded-windows",
                    "--disable-renderer-backgrounding",
                    "--disable-ipc-flooding-protection",
                    "--disable-hang-monitor",
                    "--disable-client-side-phishing-detection",
                    "--disable-popup-blocking",
                    "--disable-default-apps",
                    "--disable-prompt-on-repost",
                    "--disable-component-update",
                    "--disable-background-networking",
                    "--disable-sync",
                    "--no-first-run",
                    "--disable-extensions",
                    "--disable-plugins",
                    "--disable-images",
                    "--disable-javascript-harmony-shipping",
                    "--disable-background-mode",
                    "--disable-domain-reliability",
                    "--disable-client-side-phishing-detection",
                    "--disable-component-extensions-with-background-pages"
                ]
            });

            // Set global browser context properties
            var contexts = _browser?.Contexts;
            if (contexts is { Count: > 0 })
            {
                // Remove webdriver property
                await contexts[0].AddInitScriptAsync("""
                    Object.defineProperty(navigator, 'webdriver', {
                        get: () => undefined,
                    });

                    // Override the plugins property to use a custom getter
                    Object.defineProperty(navigator, 'plugins', {
                        get: () => [1, 2, 3, 4, 5],
                    });

                    // Override the languages property to use a custom getter
                    Object.defineProperty(navigator, 'languages', {
                        get: () => ['en-US', 'en'],
                    });

                    // Override the permissions property
                    const originalQuery = window.navigator.permissions.query;
                    window.navigator.permissions.query = (parameters) => (
                        parameters.name === 'notifications' ?
                            Promise.resolve({ state: Notification.permission }) :
                            originalQuery(parameters)
                    );
                """);
            }
        }
    }

    private static async Task SetupCloudflareBypassAsync(IPage page)
    {
        // Set realistic viewport
        await page.SetViewportSizeAsync(1920, 1080);

        // Set extra headers to appear more like a real browser (including User-Agent)
        await page.SetExtraHTTPHeadersAsync(new Dictionary<string, string>
        {
            ["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
            ["Accept"] = "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8",
            ["Accept-Language"] = "en-US,en;q=0.9",
            ["Accept-Encoding"] = "gzip, deflate, br",
            ["DNT"] = "1",
            ["Connection"] = "keep-alive",
            ["Upgrade-Insecure-Requests"] = "1",
            ["Sec-Fetch-Dest"] = "document",
            ["Sec-Fetch-Mode"] = "navigate",
            ["Sec-Fetch-Site"] = "none",
            ["Sec-Fetch-User"] = "?1",
            ["Cache-Control"] = "max-age=0"
        });

        // Add some randomness to timing
        await page.WaitForTimeoutAsync(Random.Shared.Next(1000, 3000));
    }

    private async Task WaitForCloudflareBypassAsync(IPage page)
    {
        try
        {
            // Check if we're on a Cloudflare challenge page
            var cloudflareSelectors = new[]
            {
                "[data-testid='cf-please-wait']",
                ".cf-browser-verification",
                "#cf-please-wait",
                ".cf-checking-browser",
                "div[class*='cloudflare']",
                "div[class*='cf-']"
            };

            var isCloudflareChallenge = false;
            foreach (var selector in cloudflareSelectors)
            {
                try
                {
                    var element = await page.QuerySelectorAsync(selector);
                    if (element == null) continue;
                    isCloudflareChallenge = true;
                    logger?.LogInformation("Cloudflare challenge detected, waiting for bypass...");
                    break;
                }
                catch
                {
                    // Ignore selector errors
                }
            }

            if (isCloudflareChallenge)
            {
                // Wait for the challenge to complete - use WaitForURLAsync instead of deprecated WaitForNavigationAsync
                try
                {
                    await page.WaitForURLAsync("**", new PageWaitForURLOptions
                    {
                        Timeout = 30000,
                        WaitUntil = WaitUntilState.DOMContentLoaded
                    });
                }
                catch (TimeoutException)
                {
                    // If URL doesn't change, wait for challenge elements to disappear
                    await page.WaitForSelectorAsync("[data-testid='cf-please-wait']", new PageWaitForSelectorOptions
                    {
                        State = WaitForSelectorState.Detached,
                        Timeout = 30000
                    });
                }

                // Additional wait for content to load after challenge
                await page.WaitForTimeoutAsync(3000);
            }
            else
            {
                // Standard wait for dynamic content
                await page.WaitForTimeoutAsync(2000);
            }

            // Perform some human-like actions to appear more legitimate
            await SimulateHumanBehaviorAsync(page);
        }
        catch (TimeoutException)
        {
            logger?.LogWarning("Timeout waiting for Cloudflare challenge completion, proceeding anyway");
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Error during Cloudflare bypass, proceeding anyway");
        }
    }

    private static async Task SimulateHumanBehaviorAsync(IPage page)
    {
        try
        {
            // Simulate mouse movement
            await page.Mouse.MoveAsync(Random.Shared.Next(100, 800), Random.Shared.Next(100, 600));
            await page.WaitForTimeoutAsync(Random.Shared.Next(500, 1500));

            // Simulate scroll
            await page.EvaluateAsync("window.scrollBy(0, 100)");
            await page.WaitForTimeoutAsync(Random.Shared.Next(500, 1000));
        }
        catch
        {
            // Ignore errors in human simulation
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser != null)
        {
            await _browser.CloseAsync();
            _browser = null;
        }

        _playwright?.Dispose();
        _playwright = null;

        SuppressFinalize(this);
    }
}
