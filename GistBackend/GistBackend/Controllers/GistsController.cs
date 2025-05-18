using GistBackend.Handler.MariaDbHandler;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace GistBackend.Controllers;

public static class RoutingConstants
{
    public const string GistsRoute = "/api/v1/gists";
}

[ApiController]
[Route(RoutingConstants.GistsRoute)]
public class GistsController(IMariaDbHandler mariaDbHandler, ILogger<GistsController>? logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetGistsQueryAsync(
        [FromQuery] int take = 20,
        [FromQuery] int? lastGist = null,
        [FromQuery] string? tags = null,
        [FromQuery] string? q = null,
        [FromQuery] string? disabledFeeds = null,
        CancellationToken ct = default)
    {
        try
        {
            var gists = await mariaDbHandler.GetPreviousGistsAsync(take, lastGist, ParseTags(tags), q,
                ParseDisabledFeeds(disabledFeeds), ct);
            return Ok(gists);
        }
        catch (Exception e)
        {
            return Problem("oops");
        }
    }

    private static List<string> ParseTags(string? tags) => string.IsNullOrWhiteSpace(tags)
        ? []
        : tags.Split(";;").Select(tag => tag.Trim()).ToList();

    private static List<int> ParseDisabledFeeds(string? disabledFeeds) => string.IsNullOrWhiteSpace(disabledFeeds)
        ? []
        : disabledFeeds
            .Split(',')
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Select(int.Parse)
            .ToList();
}
