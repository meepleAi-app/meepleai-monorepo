using Api.Models;
using Api.SharedKernel.Application.Interfaces;
using StackExchange.Redis;

namespace Api.BoundedContexts.Administration.Application.Queries.Resources;

/// <summary>
/// Handler for cache metrics query.
/// Uses Redis INFO command to retrieve memory and keyspace statistics.
/// Issue #3695: Resources Monitoring - Cache metrics
/// </summary>
internal class GetCacheMetricsQueryHandler : IQueryHandler<GetCacheMetricsQuery, CacheMetricsDto>
{
    private readonly IConnectionMultiplexer _redis;

    public GetCacheMetricsQueryHandler(IConnectionMultiplexer redis)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
    }

    public async Task<CacheMetricsDto> Handle(GetCacheMetricsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var endpoints = _redis.GetEndPoints();
        var server = _redis.GetServer(endpoints[0]);
        var info = await server.InfoAsync("all").ConfigureAwait(false);

        // Parse memory stats - InfoAsync returns IGrouping<string, KeyValuePair<string, string>>[]
        var usedMemory = ParseInfoValue(info, "Memory", "used_memory", 0L);
        var maxMemory = ParseInfoValue(info, "Memory", "maxmemory", 0L);

        // Parse stats section
        var keyspaceHits = ParseInfoValue(info, "Stats", "keyspace_hits", 0L);
        var keyspaceMisses = ParseInfoValue(info, "Stats", "keyspace_misses", 0L);
        var evictedKeys = ParseInfoValue(info, "Stats", "evicted_keys", 0L);
        var expiredKeys = ParseInfoValue(info, "Stats", "expired_keys", 0L);

        // Parse keyspace section for key count
        var totalKeys = ParseKeyspaceKeys(info);

        // Calculate hit rate
        var totalRequests = keyspaceHits + keyspaceMisses;
        var hitRate = totalRequests > 0 ? (double)keyspaceHits / totalRequests : 0;

        // Calculate memory usage percentage
        var memoryUsagePercent = maxMemory > 0 ? (double)usedMemory / maxMemory * 100 : 0;

        return new CacheMetricsDto(
            UsedMemoryBytes: usedMemory,
            UsedMemoryFormatted: FormatBytes(usedMemory),
            MaxMemoryBytes: maxMemory,
            MaxMemoryFormatted: maxMemory > 0 ? FormatBytes(maxMemory) : "Unlimited",
            MemoryUsagePercent: memoryUsagePercent,
            TotalKeys: totalKeys,
            KeyspaceHits: keyspaceHits,
            KeyspaceMisses: keyspaceMisses,
            HitRate: hitRate,
            EvictedKeys: evictedKeys,
            ExpiredKeys: expiredKeys,
            MeasuredAt: DateTime.UtcNow
        );
    }

    private static long ParseInfoValue(
        IGrouping<string, KeyValuePair<string, string>>[] info,
        string section,
        string key,
        long defaultValue)
    {
        var sectionGroup = info.FirstOrDefault(g => string.Equals(g.Key, section, StringComparison.Ordinal));
        if (sectionGroup == null) return defaultValue;

        var kvp = sectionGroup.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.Ordinal));
        if (kvp.Key == null) return defaultValue;

        return long.TryParse(kvp.Value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : defaultValue;
    }

    private static long ParseKeyspaceKeys(IGrouping<string, KeyValuePair<string, string>>[] info)
    {
        var keyspaceSection = info.FirstOrDefault(g => string.Equals(g.Key, "Keyspace", StringComparison.Ordinal));
        if (keyspaceSection == null) return 0;

        long totalKeys = 0;
        foreach (var kvp in keyspaceSection)
        {
            // Format: db0:keys=123,expires=45,avg_ttl=67890
            if (kvp.Key.StartsWith("db", StringComparison.Ordinal) && kvp.Value != null)
            {
                var parts = kvp.Value.Split(',');
                foreach (var part in parts)
                {
                    if (part.StartsWith("keys=", StringComparison.Ordinal))
                    {
                        var keysStr = part["keys=".Length..];
                        if (long.TryParse(keysStr, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var keys))
                        {
                            totalKeys += keys;
                        }
                    }
                }
            }
        }
        return totalKeys;
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
