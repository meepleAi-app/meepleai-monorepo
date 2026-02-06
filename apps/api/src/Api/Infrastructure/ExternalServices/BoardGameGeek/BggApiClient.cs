using System.Net;
using System.Text.Json;
using Api.Infrastructure.ExternalServices.BoardGameGeek.Models;
using Api.Middleware.Exceptions;
using Microsoft.Extensions.Caching.Distributed;

namespace Api.Infrastructure.ExternalServices.BoardGameGeek;

public class BggApiClient : IBggApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IDistributedCache _cache;

    private static readonly TimeSpan SearchCacheTtl = TimeSpan.FromHours(1);
    private static readonly TimeSpan DetailsCacheTtl = TimeSpan.FromHours(24);

    public BggApiClient(HttpClient httpClient, IDistributedCache cache)
    {
        _httpClient = httpClient;
        _cache = cache;
    }

    public async Task<List<BggSearchResult>> SearchGamesAsync(string query, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"bgg:search:{query.ToLowerInvariant()}";
        var cached = await _cache.GetStringAsync(cacheKey, cancellationToken).ConfigureAwait(false);

        if (cached != null)
            return JsonSerializer.Deserialize<List<BggSearchResult>>(cached)!;

        var response = await _httpClient.GetAsync($"search?query={Uri.EscapeDataString(query)}&type=boardgame", cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.Accepted)
            throw new HttpRequestException("BGG throttled");

        response.EnsureSuccessStatusCode();
        var xml = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var results = BggXmlParser.ParseSearchResults(xml);

        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(results),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = SearchCacheTtl }, cancellationToken).ConfigureAwait(false);

        return results;
    }

    public async Task<BggGameDetails> GetGameDetailsAsync(int bggId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"bgg:details:{bggId}";
        var cached = await _cache.GetStringAsync(cacheKey, cancellationToken).ConfigureAwait(false);

        if (cached != null)
            return JsonSerializer.Deserialize<BggGameDetails>(cached)!;

        var response = await _httpClient.GetAsync($"thing?id={bggId}&type=boardgame&stats=1", cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.Accepted)
            throw new HttpRequestException("BGG throttled");

        response.EnsureSuccessStatusCode();
        var xml = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var details = BggXmlParser.ParseGameDetails(xml);

        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(details),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = DetailsCacheTtl }, cancellationToken).ConfigureAwait(false);

        return details;
    }
}
