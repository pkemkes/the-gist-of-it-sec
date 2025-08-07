using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace GistBackend.Handlers.RssHandlers;

public static class CloudFlareBypassing
{
    public static async Task SetupCloudflareBypassAsync(IPage page)
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

    public static async Task WaitForCloudflareBypassAsync(IPage page, ILogger? logger)
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
}
