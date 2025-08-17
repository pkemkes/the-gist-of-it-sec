using System.Text.Json;
using GistBackend.Handlers.ChromaDbHandler;
using GistBackend.Handlers.MariaDbHandler;
using GistBackend.Types;
using GistBackend.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
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
    [FromKeyedServices(StartUp.GistsControllerMariaDbHandlerOptionsName)] IMariaDbHandler mariaDbHandler,
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
            var gists = await mariaDbHandler.GetPreviousGistsWithFeedAsync(take, lastGist, ParseTags(tags), q,
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
    public async Task<IActionResult> GetGistWithFeedByIdAsync(int id, CancellationToken ct = default)
    {
        try
        {
            var gistWithFeed = await mariaDbHandler.GetGistWithFeedByIdAsync(id, ct);
            if (gistWithFeed is null) return NotFound();
            return Ok(gistWithFeed);
        }
        catch (Exception e)
        {
            const string message = "Could not get gist with feed by ID from the database";
            logger?.LogError(ErrorInHttpRequest, e, message);
            return Problem(message);
        }
    }

    [HttpGet("{id:int}/similar")]
    public async Task<IActionResult> GetSimilarGistsWithFeedAsync(int id, [FromQuery] string? disabledFeeds = null,
        CancellationToken ct = default)
    {
        try
        {
            var gistWithFeed = await mariaDbHandler.GetGistWithFeedByIdAsync(id, ct);
            if (gistWithFeed is null) return NotFound();
            var similarityResults =
                await chromaDbHandler.GetReferenceAndScoreOfSimilarEntriesAsync(gistWithFeed.Reference, 5,
                    ParseDisabledFeeds(disabledFeeds), ct);
            var gistsWithFeed = new List<SimilarGistWithFeed>();
            foreach (var similarityResult in similarityResults)
            {
                gistsWithFeed.Add(await GetSimilarGistWithFeedFromDatabaseAsync(similarityResult, ct));
            }
            return Ok(gistsWithFeed);
        }
        catch (Exception e)
        {
            const string message = "Could not get similar gists from the database";
            logger?.LogError(ErrorInHttpRequest, e, message);
            return Problem(message);
        }
    }

    private async Task<SimilarGistWithFeed> GetSimilarGistWithFeedFromDatabaseAsync(SimilarDocument similarDocument,
        CancellationToken ct)
    {
        var gistWithFeed = await mariaDbHandler.GetGistWithFeedByReference(similarDocument.Reference, ct);
        if (gistWithFeed is null)
        {
            throw new KeyNotFoundException(
                $"Similar gist with reference {similarDocument.Reference} not found in database");
        }
        return new SimilarGistWithFeed(gistWithFeed, similarDocument.Similarity);
    }

    [HttpGet("{id:int}/searchResults")]
    public async Task<IActionResult> GetSearchResultsAsync(int id, CancellationToken ct)
    {
        try
        {
            var searchResults = await mariaDbHandler.GetSearchResultsByGistIdAsync(id, ct);
            return Ok(searchResults);
        }
        catch (Exception e)
        {
            const string message = "Could not get search results for gist from the database";
            logger?.LogError(ErrorInHttpRequest, e, message);
            return Problem(message);
        }
    }

    [HttpGet("feeds")]
    public async Task<IActionResult> GetAllFeedsAsync(CancellationToken ct)
    {
        try
        {
            var feeds = await mariaDbHandler.GetAllFeedInfosAsync(ct);
            return Ok(feeds);
        }
        catch (Exception e)
        {
            const string message = "Could not get all feeds from the database";
            logger?.LogError(ErrorInHttpRequest, e, message);
            return Problem(message);
        }
    }

    [HttpGet("recap/daily")]
    public Task<IActionResult> GetDailyRecapAsync(CancellationToken ct) => GetRecapAsync(RecapType.Daily, ct);

    [HttpGet("recap/weekly")]
    public Task<IActionResult> GetWeeklyRecapAsync(CancellationToken ct) => GetRecapAsync(RecapType.Weekly, ct);

    private async Task<IActionResult> GetRecapAsync(RecapType type, CancellationToken ct)
    {
        try
        {
            var serializedRecap = await mariaDbHandler.GetLatestRecapAsync(type, ct);
            if (serializedRecap is null) return NotFound("No recap found in the database");
            var recap = JsonSerializer.Deserialize<Recap>(serializedRecap.Recap, SerializerDefaults.JsonOptions);
            if (recap is null)
            {
                throw new JsonException($"Could not deserialize recap from this JSON: {serializedRecap.Recap}");
            }
            var gistTitlesById = new Dictionary<int, string>();
            var gistIds = recap.RecapSections.SelectMany(s => s.Related).Distinct().ToList();
            foreach (var gistId in gistIds)
            {
                var gistWithFeed = await mariaDbHandler.GetGistWithFeedByIdAsync(gistId, ct);
                if (gistWithFeed is not null) gistTitlesById[gistId] = gistWithFeed.Title;
            }

            var deserializedRecapSections = recap.RecapSections.Select(s => new DeserializedRecapSection(s.Heading,
                s.Recap,
                s.Related.Where(r => gistTitlesById.ContainsKey(r))
                    .Select(r => new RelatedGistInfo(r, gistTitlesById[r]))));
            var deserializedRecap = new DeserializedRecap(serializedRecap.Created, deserializedRecapSections,
                serializedRecap.Id ?? -1);
            return Ok(deserializedRecap);
        }
        catch (Exception e)
        {
            const string message = "Could not get recap from the database";
            logger?.LogError(ErrorInHttpRequest, e, message);
            return Problem(message);
        }
    }

    private static List<string> ParseTags(string? tags) => string.IsNullOrWhiteSpace(tags)
        ? []
        : tags
            .Split(";;", StringSplitOptions.RemoveEmptyEntries)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(tag => tag.Trim()).ToList();

    private static List<int> ParseDisabledFeeds(string? disabledFeeds) => string.IsNullOrWhiteSpace(disabledFeeds)
        ? []
        : disabledFeeds
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Select(int.Parse)
            .ToList();
}
