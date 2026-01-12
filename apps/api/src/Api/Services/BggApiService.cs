using System.Globalization;
using System.Web;
using System.Xml.Linq;
using Api.Helpers;
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
internal class BggApiService : IBggApiService
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
        var cachedResults = await _cache.GetOrCreateAsync<List<BggSearchResultDto>?>(
            cacheKey,
            async cancel =>
            {
                _logger.LogInformation("BGG search cache miss for query: {Query}", query);
                return await FetchSearchResultsAsync(query, exact, cancel).ConfigureAwait(false);
            },
            new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromDays(_config.CacheTtlDays),
                LocalCacheExpiration = TimeSpan.FromHours(1)
            },
            cancellationToken: ct).ConfigureAwait(false);

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
        var cachedDetails = await _cache.GetOrCreateAsync<BggGameDetailsDto?>(
            cacheKey,
            async cancel =>
            {
                _logger.LogInformation("BGG game details cache miss for ID: {BggId}", bggId);
                return await FetchGameDetailsAsync(bggId, cancel).ConfigureAwait(false);
            },
            new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromDays(_config.CacheTtlDays),
                LocalCacheExpiration = TimeSpan.FromHours(1)
            },
            cancellationToken: ct).ConfigureAwait(false);

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
            ct).ConfigureAwait(false);

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

            // CODE-01: Dispose HttpResponseMessage to prevent resource leak (CWE-404)
            using var response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var xmlContent = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var doc = XDocument.Parse(xmlContent);

            var results = doc.Root?
                .Elements("item")
                .Select(item => ParseSearchResult(item))
                .Where(result => result != null)
                .Cast<BggSearchResultDto>()
                .ToList();

            _logger.LogInformation("BGG search returned {Count} results for query: {Query}", results?.Count ?? 0, query);
            return results;
        }
        catch (HttpRequestException ex)
        {
            // Issue #1444: Use centralized logging for HTTP failures, then wrap and re-throw
            _logger.LogError(ex, "BGG API request failed for search query: {Query}", query);
            throw new InvalidOperationException("BoardGameGeek API is currently unavailable", ex);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // Justification: Service boundary - BGG API integration with optional result pattern, gracefully handles XML parsing errors
        // Issue #1444: Use centralized exception handling for optional result pattern (nullable)
        catch (Exception ex)
        {
            return RagExceptionHandler.HandleServiceException<List<BggSearchResultDto>?>(
                ex, _logger, $"parsing BGG search results for query: {query}",
                _ => null);
        }
#pragma warning restore CA1031
    }

    private async Task<BggGameDetailsDto?> FetchGameDetailsAsync(int bggId, CancellationToken ct)
    {
        // Apply rate limiting
        var rateLimitResult = await _rateLimitService.CheckRateLimitAsync(
            RateLimitKey,
            maxTokens: 10,
            refillRate: _config.MaxRequestsPerSecond,
            ct).ConfigureAwait(false);

        if (!rateLimitResult.Allowed)
        {
            _logger.LogWarning("BGG rate limit exceeded. Retry after {RetryAfter}s", rateLimitResult.RetryAfterSeconds);
            throw new InvalidOperationException($"Rate limit exceeded. Retry after {rateLimitResult.RetryAfterSeconds} seconds");
        }

        try
        {
            var url = $"{_config.BaseUrl}/thing?id={bggId}&stats=1";

            _logger.LogInformation("Fetching BGG game details from: {Url}", url);

            // CODE-01: Dispose HttpResponseMessage to prevent resource leak (CWE-404)
            using var response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var xmlContent = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
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
            // Issue #1444: Use centralized logging for HTTP failures, then wrap and re-throw
            _logger.LogError(ex, "BGG API request failed for game ID: {BggId}", bggId);
            throw new InvalidOperationException("BoardGameGeek API is currently unavailable", ex);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // Justification: Service boundary - BGG API integration with optional result pattern, gracefully handles XML parsing errors
        // Issue #1444: Use centralized exception handling for optional result pattern (nullable)
        catch (Exception ex)
        {
            return RagExceptionHandler.HandleServiceException<BggGameDetailsDto?>(
                ex, _logger, $"parsing BGG game details for ID: {bggId}",
                _ => null);
        }
#pragma warning restore CA1031
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
#pragma warning disable CA1031 // Do not catch general exception types
        // Justification: Infrastructure adapter - Gracefully handles malformed BGG XML data, skips invalid search results without failing entire operation
        // Issue #1444: Graceful handling of malformed BGG API data
        catch (Exception ex) when (ex is FormatException or OverflowException)
        {
            // BGG API occasionally returns malformed data - skip this result (log at Debug level)
            _logger.LogDebug(ex, "Skipped malformed BGG search result. Item ID attribute: {ItemId}",
                item.Attribute("id")?.Value ?? "unknown");
            return null;
        }
#pragma warning restore CA1031
    }

    private BggGameDetailsDto? ParseGameDetails(XElement item, int bggId)
    {
        try
        {
            // Primary name (type="primary")
            var primaryName = item.Elements("name")
                .FirstOrDefault(n => string.Equals(n.Attribute("type")?.Value, "primary", StringComparison.Ordinal))
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
                .Where(l => string.Equals(l.Attribute("type")?.Value, "boardgamecategory", StringComparison.Ordinal))
                .Select(l => l.Attribute("value")?.Value)
                .Where(v => !string.IsNullOrEmpty(v))
                .Cast<string>()
                .ToList();

            var mechanics = item.Elements("link")
                .Where(l => string.Equals(l.Attribute("type")?.Value, "boardgamemechanic", StringComparison.Ordinal))
                .Select(l => l.Attribute("value")?.Value)
                .Where(v => !string.IsNullOrEmpty(v))
                .Cast<string>()
                .ToList();

            var designers = item.Elements("link")
                .Where(l => string.Equals(l.Attribute("type")?.Value, "boardgamedesigner", StringComparison.Ordinal))
                .Select(l => l.Attribute("value")?.Value)
                .Where(v => !string.IsNullOrEmpty(v))
                .Cast<string>()
                .ToList();

            var publishers = item.Elements("link")
                .Where(l => string.Equals(l.Attribute("type")?.Value, "boardgamepublisher", StringComparison.Ordinal))
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
#pragma warning disable CA1031 // Do not catch general exception types
        // Justification: Infrastructure adapter - Gracefully handles malformed BGG XML data, returns null for invalid game details without failing entire operation
        // Issue #1444: Graceful handling of malformed BGG API data
        catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException)
        {
            // BGG API occasionally returns malformed data (log at Debug level to avoid noise)
            _logger.LogDebug(ex, "Failed to parse BGG game details for ID {BggId}. Data may be malformed in BGG API response", bggId);
            return null;
        }
#pragma warning restore CA1031
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
