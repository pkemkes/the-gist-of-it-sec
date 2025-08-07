using GistBackend.Types;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using static GistBackend.Handlers.RssHandlers.CloudFlareBypassing;

namespace GistBackend.Handlers.RssHandlers;

public interface IRssEntryHandler {
    public Task<string> FetchTextContentAsync(RssEntry entry, CancellationToken ct);
}

public class RssEntryHandler(ILogger<RssEntryHandler>? logger = null) : IRssEntryHandler, IAsyncDisposable
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public async Task<string> FetchTextContentAsync(RssEntry entry, CancellationToken ct)
    {
        try
        {
            await EnsureBrowserInitializedAsync();

            var page = await _browser!.NewPageAsync();
            try
            {
                // Set up Cloudflare bypass techniques
                await SetupCloudflareBypassAsync(page);

                // Navigate to the page with extended timeout for Cloudflare challenge
                await page.GotoAsync(entry.Url.ToString(), new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.DOMContentLoaded,
                    Timeout = 60000 // Extended timeout for Cloudflare challenges
                });

                // Wait for potential Cloudflare challenge to complete
                await WaitForCloudflareBypassAsync(page, logger);

                // Get the HTML content
                var pageContent = await page.ContentAsync();

                return entry.ExtractText(pageContent);
            }
            finally
            {
                await page.CloseAsync();
            }
        }
        catch (Exception e)
        {
            logger?.LogError(e, "Failed to fetch text content for entry with URL {EntryUrl}", entry.Url);
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

    public async ValueTask DisposeAsync()
    {
        if (_browser != null)
        {
            await _browser.CloseAsync();
            _browser = null;
        }

        _playwright?.Dispose();
        _playwright = null;
    }
}
