using System.Globalization;
using System.Web;
using System.Xml.Linq;
using Api.Models;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;

namespace Api.Services;

/// <summary>
/// Implementation of BoardGameGeek XML API v2 integration.
/// Provides cached search and game details retrieval with retry logic.
/// AI-13: https://github.com/DegrassiAaron/meepleai-monorepo/issues/420
/// </summary>
public class BggApiService : IBggApiService
{
    private readonly HttpClient _httpClient;
    private readonly HybridCache _cache;
    private readonly ILogger<BggApiService> _logger;
    private readonly BggConfiguration _config;
    private readonly IRateLimitService _rateLimitService;
    private const string RateLimitKey = "bgg:api:global";

    public BggApiService(
        IHttpClientFactory httpClientFactory,
        HybridCache cache,
        ILogger<BggApiService> logger,
        IOptions<BggConfiguration> config,
        IRateLimitService rateLimitService)
    {
        _httpClient = httpClientFactory.CreateClient("BggApi");
        _cache = cache;
        _logger = logger;
        _config = config.Value;
        _rateLimitService = rateLimitService;
    }

    /// <inheritdoc />
    public async Task<List<BggSearchResultDto>> SearchGamesAsync(
        string query,
        bool exact = false,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Search query cannot be empty", nameof(query));
        }

        var cacheKey = $"bgg:search:{query.ToLowerInvariant()}:{exact}";

        // Try cache first (7-day TTL by default)
        var cachedResults = await _cache.GetOrCreateAsync(
            cacheKey,
            async cancel =>
            {
                _logger.LogInformation("BGG search cache miss for query: {Query}", query);
                return await FetchSearchResultsAsync(query, exact, cancel);
            },
            new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromDays(_config.CacheTtlDays),
                LocalCacheExpiration = TimeSpan.FromHours(1)
            },
            cancellationToken: ct);

        return cachedResults ?? new List<BggSearchResultDto>();
    }

    /// <inheritdoc />
    public async Task<BggGameDetailsDto?> GetGameDetailsAsync(int bggId, CancellationToken ct = default)
    {
        if (bggId <= 0)
        {
            throw new ArgumentException("BGG ID must be positive", nameof(bggId));
        }

        var cacheKey = $"bgg:game:{bggId}";

        // Try cache first
        var cachedDetails = await _cache.GetOrCreateAsync(
            cacheKey,
            async cancel =>
            {
                _logger.LogInformation("BGG game details cache miss for ID: {BggId}", bggId);
                return await FetchGameDetailsAsync(bggId, cancel);
            },
            new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromDays(_config.CacheTtlDays),
                LocalCacheExpiration = TimeSpan.FromHours(1)
            },
            cancellationToken: ct);

        return cachedDetails;
    }

    private async Task<List<BggSearchResultDto>?> FetchSearchResultsAsync(
        string query,
        bool exact,
        CancellationToken ct)
    {
        // Apply rate limiting (max 2 requests/second to BGG)
        var rateLimitResult = await _rateLimitService.CheckRateLimitAsync(
            RateLimitKey,
            maxTokens: 10,
            refillRate: _config.MaxRequestsPerSecond,
            ct);

        if (!rateLimitResult.Allowed)
        {
            _logger.LogWarning("BGG rate limit exceeded. Retry after {RetryAfter}s", rateLimitResult.RetryAfterSeconds);
            throw new InvalidOperationException($"Rate limit exceeded. Retry after {rateLimitResult.RetryAfterSeconds} seconds");
        }

        try
        {
            var encodedQuery = HttpUtility.UrlEncode(query);
            var exactParam = exact ? "&exact=1" : "";
            var url = $"{_config.BaseUrl}/search?query={encodedQuery}&type=boardgame{exactParam}";

            _logger.LogInformation("Fetching BGG search results from: {Url}", url);

            var response = await _httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var xmlContent = await response.Content.ReadAsStringAsync(ct);
            var doc = XDocument.Parse(xmlContent);

            var results = doc.Root?
                .Elements("item")
                .Take(5) // Top 5 results only
                .Select(item => ParseSearchResult(item))
                .Where(result => result != null)
                .Cast<BggSearchResultDto>()
                .ToList();

            _logger.LogInformation("BGG search returned {Count} results for query: {Query}", results?.Count ?? 0, query);
            return results;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "BGG API request failed for search query: {Query}", query);
            throw new InvalidOperationException("BoardGameGeek API is currently unavailable", ex);
        }
        catch (Exception ex)
        {
            // EXTERNAL API PATTERN: BGG XML parsing errors return null instead of throwing
            // Rationale: BGG API search is already successfully completed - we received XML from
            // their server. Parsing failures indicate malformed/unexpected XML structure, not a
            // system error. Returning null allows callers to gracefully handle "no results found"
            // scenarios. HttpRequestException above handles actual API connectivity failures.
            // Context: Parsing failures are typically BGG-side (schema changes, malformed XML)
            _logger.LogError(ex, "Error parsing BGG search results for query: {Query}", query);
            return null;
        }
    }

    private async Task<BggGameDetailsDto?> FetchGameDetailsAsync(int bggId, CancellationToken ct)
    {
        // Apply rate limiting
        var rateLimitResult = await _rateLimitService.CheckRateLimitAsync(
            RateLimitKey,
            maxTokens: 10,
            refillRate: _config.MaxRequestsPerSecond,
            ct);

        if (!rateLimitResult.Allowed)
        {
            _logger.LogWarning("BGG rate limit exceeded. Retry after {RetryAfter}s", rateLimitResult.RetryAfterSeconds);
            throw new InvalidOperationException($"Rate limit exceeded. Retry after {rateLimitResult.RetryAfterSeconds} seconds");
        }

        try
        {
            var url = $"{_config.BaseUrl}/thing?id={bggId}&stats=1";

            _logger.LogInformation("Fetching BGG game details from: {Url}", url);

            var response = await _httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var xmlContent = await response.Content.ReadAsStringAsync(ct);
            var doc = XDocument.Parse(xmlContent);

            var item = doc.Root?.Element("item");
            if (item == null)
            {
                _logger.LogWarning("BGG game not found: {BggId}", bggId);
                return null;
            }

            var details = ParseGameDetails(item, bggId);
            _logger.LogInformation("BGG game details retrieved for ID: {BggId}, Name: {Name}", bggId, details?.Name);
            return details;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "BGG API request failed for game ID: {BggId}", bggId);
            throw new InvalidOperationException("BoardGameGeek API is currently unavailable", ex);
        }
        catch (Exception ex)
        {
            // EXTERNAL API PATTERN: BGG XML parsing errors return null instead of throwing
            // Rationale: BGG API request is already successfully completed - we received XML from
            // their server. Parsing failures indicate malformed/unexpected XML structure, not a
            // system error. Returning null allows callers to gracefully handle "game not found"
            // scenarios. HttpRequestException above handles actual API connectivity failures.
            // Context: Parsing failures are typically BGG-side (schema changes, malformed XML)
            _logger.LogError(ex, "Error parsing BGG game details for ID: {BggId}", bggId);
            return null;
        }
    }

    private BggSearchResultDto? ParseSearchResult(XElement item)
    {
        try
        {
            var id = (int?)item.Attribute("id");
            var name = item.Element("name")?.Attribute("value")?.Value;
            var yearPublished = (int?)item.Element("yearpublished")?.Attribute("value");
            var thumbnail = item.Element("thumbnail")?.Value;
            var type = item.Attribute("type")?.Value ?? "boardgame";

            if (id == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            return new BggSearchResultDto(
                id.Value,
                name,
                yearPublished,
                thumbnail,
                type
            );
        }
        catch (Exception ex) when (ex is FormatException or OverflowException)
        {
            // BGG API occasionally returns malformed data - skip this result
            // Logging at Debug level to avoid noise from external API issues
            _logger.LogDebug(
                ex,
                "Skipped malformed BGG search result. Item ID attribute: {ItemId}",
                item.Attribute("id")?.Value ?? "unknown");
            return null;
        }
    }

    private BggGameDetailsDto? ParseGameDetails(XElement item, int bggId)
    {
        try
        {
            // Primary name (type="primary")
            var primaryName = item.Elements("name")
                .FirstOrDefault(n => n.Attribute("type")?.Value == "primary")
                ?.Attribute("value")?.Value;

            var name = primaryName ?? item.Element("name")?.Attribute("value")?.Value;
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            // Description (may contain HTML)
            var description = item.Element("description")?.Value;

            // Basic metadata
            var yearPublished = (int?)item.Element("yearpublished")?.Attribute("value");
            var minPlayers = (int?)item.Element("minplayers")?.Attribute("value");
            var maxPlayers = (int?)item.Element("maxplayers")?.Attribute("value");
            var playingTime = (int?)item.Element("playingtime")?.Attribute("value");
            var minPlayTime = (int?)item.Element("minplaytime")?.Attribute("value");
            var maxPlayTime = (int?)item.Element("maxplaytime")?.Attribute("value");
            var minAge = (int?)item.Element("minage")?.Attribute("value");

            // Images
            var thumbnail = item.Element("thumbnail")?.Value;
            var image = item.Element("image")?.Value;

            // Ratings and stats
            var stats = item.Element("statistics")?.Element("ratings");
            var averageRating = ParseDouble(stats?.Element("average")?.Attribute("value")?.Value);
            var bayesAverageRating = ParseDouble(stats?.Element("bayesaverage")?.Attribute("value")?.Value);
            var usersRated = (int?)stats?.Element("usersrated")?.Attribute("value");
            var averageWeight = ParseDouble(stats?.Element("averageweight")?.Attribute("value")?.Value);

            // Categories, Mechanics, Designers, Publishers
            var categories = item.Elements("link")
                .Where(l => l.Attribute("type")?.Value == "boardgamecategory")
                .Select(l => l.Attribute("value")?.Value)
                .Where(v => !string.IsNullOrEmpty(v))
                .Cast<string>()
                .ToList();

            var mechanics = item.Elements("link")
                .Where(l => l.Attribute("type")?.Value == "boardgamemechanic")
                .Select(l => l.Attribute("value")?.Value)
                .Where(v => !string.IsNullOrEmpty(v))
                .Cast<string>()
                .ToList();

            var designers = item.Elements("link")
                .Where(l => l.Attribute("type")?.Value == "boardgamedesigner")
                .Select(l => l.Attribute("value")?.Value)
                .Where(v => !string.IsNullOrEmpty(v))
                .Cast<string>()
                .ToList();

            var publishers = item.Elements("link")
                .Where(l => l.Attribute("type")?.Value == "boardgamepublisher")
                .Select(l => l.Attribute("value")?.Value)
                .Where(v => !string.IsNullOrEmpty(v))
                .Cast<string>()
                .ToList();

            return new BggGameDetailsDto(
                bggId,
                name,
                description,
                yearPublished,
                minPlayers,
                maxPlayers,
                playingTime,
                minPlayTime,
                maxPlayTime,
                minAge,
                averageRating,
                bayesAverageRating,
                usersRated,
                averageWeight,
                thumbnail,
                image,
                categories,
                mechanics,
                designers,
                publishers
            );
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException)
        {
            // BGG API occasionally returns malformed data - caller will log this
            // Only log at Debug level to avoid duplicate errors
            _logger.LogDebug(
                ex,
                "Failed to parse BGG game details for ID {BggId}. Data may be malformed in BGG API response",
                bggId);
            return null;
        }
    }

    private static double? ParseDouble(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        return null;
    }
}
