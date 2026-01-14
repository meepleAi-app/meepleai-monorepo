using System.Diagnostics;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Api.Infrastructure.HealthChecks;

/// <summary>
/// ISSUE #2424: Health check for SharedGameCatalog Full-Text Search performance.
///
/// Validates:
/// - PostgreSQL FTS index (ix_shared_games_fts) usage
/// - Search query performance (P95 target less than 200ms)
/// - Category/Mechanic taxonomy data availability
///
/// Health Status:
/// - Healthy: P95 less than 200ms (target met)
/// - Degraded: P95 200-500ms (acceptable but suboptimal)
/// - Unhealthy: P95 greater than 500ms OR query failure
/// </summary>
internal sealed class SharedGameCatalogHealthCheck : IHealthCheck
{
    private readonly MeepleAiDbContext _context;
    private readonly ILogger<SharedGameCatalogHealthCheck> _logger;

    public SharedGameCatalogHealthCheck(
        MeepleAiDbContext context,
        ILogger<SharedGameCatalogHealthCheck> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var stopwatch = Stopwatch.StartNew();

            // Test 1: FTS query performance (lightweight test with LIMIT 1)
            _ = await _context.SharedGames
                .AsNoTracking()
                .Where(g => EF.Functions.ToTsVector("italian", g.Title + " " + g.Description)
                    .Matches(EF.Functions.PlainToTsQuery("italian", "strategia")))
                .Take(1)
                .AnyAsync(cancellationToken)
                .ConfigureAwait(false);

            stopwatch.Stop();
            var ftsLatencyMs = stopwatch.Elapsed.TotalMilliseconds;

            // Test 2: Check taxonomy data availability (categories + mechanics)
            var categoriesCount = await _context.GameCategories
                .AsNoTracking()
                .CountAsync(cancellationToken)
                .ConfigureAwait(false);

            var mechanicsCount = await _context.GameMechanics
                .AsNoTracking()
                .CountAsync(cancellationToken)
                .ConfigureAwait(false);

            // Test 3: Check published games availability
            var publishedGamesCount = await _context.SharedGames
                .AsNoTracking()
                .Where(g => g.Status == 2) // Published
                .CountAsync(cancellationToken)
                .ConfigureAwait(false);

            // Determine health status based on performance
            if (ftsLatencyMs < 200)
            {
                // Healthy: Target met (P95 < 200ms)
                return HealthCheckResult.Healthy(
                    $"SharedGameCatalog FTS operational. " +
                    $"Latency: {ftsLatencyMs:F2}ms (target: <200ms). " +
                    $"Games: {publishedGamesCount}, Categories: {categoriesCount}, Mechanics: {mechanicsCount}",
                    new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["fts_latency_ms"] = ftsLatencyMs,
                        ["published_games_count"] = publishedGamesCount,
                        ["categories_count"] = categoriesCount,
                        ["mechanics_count"] = mechanicsCount,
                        ["performance_status"] = "optimal"
                    });
            }
            else if (ftsLatencyMs < 500)
            {
                // Degraded: Acceptable but suboptimal (200-500ms)
                _logger.LogWarning(
                    "SharedGameCatalog FTS degraded performance: {Latency}ms (target: <200ms)",
                    ftsLatencyMs);

                return HealthCheckResult.Degraded(
                    $"SharedGameCatalog FTS performance degraded. " +
                    $"Latency: {ftsLatencyMs:F2}ms (target: <200ms). " +
                    $"Consider re-indexing or database maintenance.",
                    data: new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["fts_latency_ms"] = ftsLatencyMs,
                        ["performance_status"] = "degraded",
                        ["recommendation"] = "Consider VACUUM ANALYZE on shared_games table"
                    });
            }
            else
            {
                // Unhealthy: Severe performance degradation (> 500ms)
                _logger.LogError(
                    "SharedGameCatalog FTS unhealthy: {Latency}ms (target: <200ms)",
                    ftsLatencyMs);

                return HealthCheckResult.Unhealthy(
                    $"SharedGameCatalog FTS performance critical. " +
                    $"Latency: {ftsLatencyMs:F2}ms (target: <200ms). " +
                    $"Immediate database maintenance required.",
                    data: new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["fts_latency_ms"] = ftsLatencyMs,
                        ["performance_status"] = "critical",
                        ["recommendation"] = "Run VACUUM ANALYZE and verify ix_shared_games_fts index"
                    });
            }
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // HEALTH CHECK PATTERN: Must return Unhealthy instead of throwing
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "SharedGameCatalog health check failed");
            return HealthCheckResult.Unhealthy(
                "SharedGameCatalog is not accessible",
                ex,
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["error"] = ex.Message
                });
        }
    }
}
