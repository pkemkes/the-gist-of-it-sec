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
        [FromQuery] LanguageMode? languageMode = null,
        CancellationToken ct = default)
    {
        try
        {
            var gists = await mariaDbHandler.GetPreviousConstructedGistsAsync(take, lastGist, ParseTags(tags), q,
                ParseDisabledFeeds(disabledFeeds), languageMode, ct);
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
        GetGistsQueryAsync(1, null, null, null, null, null, ct);

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetGistWithFeedByIdAsync(
        int id,
        [FromQuery] LanguageMode? languageMode = null,
        CancellationToken ct = default)
    {
        try
        {
            var gist = await mariaDbHandler.GetConstructedGistByIdAsync(id, languageMode, ct);
            if (gist is null) return NotFound();
            return Ok(gist);
        }
        catch (Exception e)
        {
            const string message = "Could not get gist with feed by ID from the database";
            logger?.LogError(ErrorInHttpRequest, e, message);
            return Problem(message);
        }
    }

    [HttpGet("{id:int}/similar")]
    public async Task<IActionResult> GetSimilarGistsWithFeedAsync(
        int id,
        [FromQuery] string? disabledFeeds = null,
        [FromQuery] LanguageMode? languageMode = null,
        CancellationToken ct = default)
    {
        try
        {
            var gistWithFeed = await mariaDbHandler.GetConstructedGistByIdAsync(id, languageMode, ct);
            if (gistWithFeed is null) return NotFound();
            var similarityResults =
                await chromaDbHandler.GetReferenceAndScoreOfSimilarEntriesAsync(gistWithFeed.Reference, 5,
                    ParseDisabledFeeds(disabledFeeds), ct);
            var gistsWithFeed = new List<SimilarGistWithFeed>();
            foreach (var similarityResult in similarityResults)
            {
                gistsWithFeed.Add(await GetSimilarGistWithFeedFromDatabaseAsync(similarityResult, languageMode, ct));
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
        LanguageMode? languageMode, CancellationToken ct)
    {
        var gistWithFeed = await mariaDbHandler.GetConstructedGistByReference(similarDocument.Reference, languageMode, ct);
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
    public Task<IActionResult> GetDailyRecapAsync([FromQuery] LanguageMode languageMode, CancellationToken ct) =>
        GetRecapAsync(RecapType.Daily, languageMode, ct);

    [HttpGet("recap/weekly")]
    public Task<IActionResult> GetWeeklyRecapAsync([FromQuery] LanguageMode languageMode, CancellationToken ct) =>
        GetRecapAsync(RecapType.Weekly, languageMode, ct);

    private async Task<IActionResult> GetRecapAsync(RecapType type, LanguageMode languageMode, CancellationToken ct)
    {
        try
        {
            if (languageMode == LanguageMode.Original)
            {
                throw new ArgumentException("Language mode 'Original' is not supported for recaps");
            }
            var serializedRecap = await mariaDbHandler.GetLatestRecapAsync(type, ct);
            if (serializedRecap is null) return NotFound("No recap found in the database");
            var serializedRecapSections =
                languageMode == LanguageMode.En ? serializedRecap.RecapEn : serializedRecap.RecapDe;
            var recapSections =
                JsonSerializer.Deserialize<IEnumerable<RecapSection>>(serializedRecapSections,
                    SerializerDefaults.JsonOptions);
            if (recapSections is null)
            {
                throw new JsonException($"Could not deserialize recap from this JSON: {serializedRecapSections}");
            }

            recapSections = recapSections.ToList();
            var gistTitlesById = new Dictionary<int, string>();
            var gistIds = recapSections.SelectMany(s => s.Related).Distinct().ToList();
            foreach (var gistId in gistIds)
            {
                var gistWithFeed = await mariaDbHandler.GetConstructedGistByIdAsync(gistId, LanguageMode.Original, ct);
                if (gistWithFeed is not null) gistTitlesById[gistId] = gistWithFeed.Title;
            }

            var deserializedRecapSections = recapSections.Select(s => new DeserializedRecapSection(s.Heading,
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
            logger?.LogError(CouldNotGetRecap, e, message);
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
