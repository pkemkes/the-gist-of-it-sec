using GistBackend.Handler.ChromaDbHandler;
using GistBackend.Handler.MariaDbHandler;
using GistBackend.Types;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using static GistBackend.Utils.LogEvents;

namespace GistBackend.Controllers;

public static class RoutingConstants
{
    public const string GistsRoute = "/api/v1/gists";
}

[ApiController]
[Route(RoutingConstants.GistsRoute)]
public class GistsController(
    IMariaDbHandler mariaDbHandler,
    IChromaDbHandler chromaDbHandler,
    ILogger<GistsController>? logger) : ControllerBase
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
            const string message = "Could not get gists from the database";
            logger?.LogError(ErrorInHttpRequest, e, message);
            return Problem(message);
        }
    }

    [HttpGet("health")]
    public Task<IActionResult> GetHealthAsync(CancellationToken ct = default) =>
        GetGistsQueryAsync(1, null, null, null, null, ct);

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetGistByIdAsync(int id, CancellationToken ct = default)
    {
        try
        {
            var gist = await mariaDbHandler.GetGistByIdAsync(id, ct);
            if (gist is null) return NotFound();
            return Ok(gist);
        }
        catch (Exception e)
        {
            const string message = "Could not get gist by ID from the database";
            logger?.LogError(ErrorInHttpRequest, e, message);
            return Problem(message);
        }
    }

    [HttpGet("similar/{id:int}")]
    public async Task<IActionResult> GetSimilarGistsAsync(int id, [FromQuery] string? disabledFeeds = null,
        CancellationToken ct = default)
    {
        try
        {
            var gist = await mariaDbHandler.GetGistByIdAsync(id, ct);
            if (gist is null) return NotFound();
            var similarityResults =
                await chromaDbHandler.GetReferenceAndScoreOfSimilarEntriesAsync(gist.Reference, 5,
                    ParseDisabledFeeds(disabledFeeds), ct);
            var gists = await Task.WhenAll(similarityResults.Select(similarityResult =>
                GetSimilarGistFromDatabaseAsync(similarityResult, ct)));
            return Ok(gists);
        }
        catch (Exception e)
        {
            const string message = "Could not get similar gists from the database";
            logger?.LogError(ErrorInHttpRequest, e, message);
            return Problem(message);
        }
    }

    private async Task<SimilarGist> GetSimilarGistFromDatabaseAsync(SimilarDocument similarDocument,
        CancellationToken ct)
    {
        var gist = await mariaDbHandler.GetGistByReferenceAsync(similarDocument.Reference, ct);
        if (gist is null)
        {
            throw new KeyNotFoundException(
                $"Similar gist with reference {similarDocument.Reference} not found in database");
        }
        return new SimilarGist(gist, similarDocument.Similarity);
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
