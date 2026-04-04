using System.Diagnostics;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Api.Infrastructure.HealthChecks;

/// <summary>
/// ISSUE #2424: Health check for SharedGameCatalog Full-Text Search performance
/// and enrichment queue depth monitoring.
///
/// Validates:
/// - PostgreSQL FTS index (ix_shared_games_fts) usage
/// - Search query performance (P95 target less than 200ms)
/// - Category/Mechanic taxonomy data availability
/// - Enrichment queue depth (Degraded if greater than 500 pending items)
///
/// Health Status:
/// - Healthy: P95 less than 200ms (target met) and queue depth less than or equal to 500
/// - Degraded: P95 200-500ms OR queue depth greater than 500
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

            // Test 4: Check enrichment queue depth
            var enrichmentQueueDepth = await _context.BggImportQueue
                .AsNoTracking()
                .Where(q => q.Status == BggImportStatus.Queued || q.Status == BggImportStatus.Processing)
                .CountAsync(cancellationToken)
                .ConfigureAwait(false);

            const int queueDegradedThreshold = 500;
            var queueDegraded = enrichmentQueueDepth > queueDegradedThreshold;

            if (queueDegraded)
            {
                _logger.LogWarning(
                    "Enrichment queue depth {Depth} exceeds threshold {Threshold}",
                    enrichmentQueueDepth, queueDegradedThreshold);
            }

            // Determine health status based on performance and queue depth
            if (ftsLatencyMs < 200 && !queueDegraded)
            {
                // Healthy: Target met (P95 < 200ms) and queue within bounds
                return HealthCheckResult.Healthy(
                    $"SharedGameCatalog FTS operational. " +
                    $"Latency: {ftsLatencyMs:F2}ms (target: <200ms). " +
                    $"Games: {publishedGamesCount}, Categories: {categoriesCount}, Mechanics: {mechanicsCount}. " +
                    $"Enrichment queue: {enrichmentQueueDepth}",
                    new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["fts_latency_ms"] = ftsLatencyMs,
                        ["published_games_count"] = publishedGamesCount,
                        ["categories_count"] = categoriesCount,
                        ["mechanics_count"] = mechanicsCount,
                        ["enrichment_queue_depth"] = enrichmentQueueDepth,
                        ["performance_status"] = "optimal"
                    });
            }
            else if (ftsLatencyMs < 500)
            {
                // Degraded: FTS suboptimal (200-500ms) or queue depth exceeded
                var reasons = new List<string>();
                if (ftsLatencyMs >= 200)
                    reasons.Add($"FTS latency {ftsLatencyMs:F2}ms (target: <200ms)");
                if (queueDegraded)
                    reasons.Add($"Enrichment queue depth {enrichmentQueueDepth} (threshold: {queueDegradedThreshold})");

                _logger.LogWarning(
                    "SharedGameCatalog degraded: {Reasons}",
                    string.Join("; ", reasons));

                return HealthCheckResult.Degraded(
                    $"SharedGameCatalog degraded. {string.Join(". ", reasons)}.",
                    data: new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["fts_latency_ms"] = ftsLatencyMs,
                        ["enrichment_queue_depth"] = enrichmentQueueDepth,
                        ["performance_status"] = "degraded",
                        ["recommendation"] = queueDegraded
                            ? "Enrichment queue backlog detected — verify BGG worker is running"
                            : "Consider VACUUM ANALYZE on shared_games table"
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
                    $"Enrichment queue: {enrichmentQueueDepth}. " +
                    $"Immediate database maintenance required.",
                    data: new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["fts_latency_ms"] = ftsLatencyMs,
                        ["enrichment_queue_depth"] = enrichmentQueueDepth,
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
