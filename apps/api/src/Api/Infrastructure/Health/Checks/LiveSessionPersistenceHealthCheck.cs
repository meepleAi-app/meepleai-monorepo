using System.Diagnostics;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Api.Infrastructure.Health.Checks;

/// <summary>
/// Verifies DB connectivity to the <c>live_game_sessions</c> table by issuing a
/// lightweight <c>COUNT(1) LIMIT 1</c> query via EF Core.
///
/// Thresholds:
///   • &lt; 1 000 ms → Healthy
///   • ≥ 1 000 ms (query succeeded) → Degraded
///   • Exception → Unhealthy
///
/// Tagged with <c>core</c> and <c>live-sessions</c> so it surfaces in both
/// the generic /health/core group and a dedicated /health/live-sessions slice.
///
/// Issue #2097 / ADR-060 Phase 4.
/// </summary>
internal sealed class LiveSessionPersistenceHealthCheck : IHealthCheck
{
    private readonly MeepleAiDbContext _db;
    private readonly ILogger<LiveSessionPersistenceHealthCheck> _logger;

    public LiveSessionPersistenceHealthCheck(
        MeepleAiDbContext db,
        ILogger<LiveSessionPersistenceHealthCheck> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var count = await _db.LiveGameSessions
                .AsNoTracking()
                .Take(1)
                .CountAsync(cancellationToken)
                .ConfigureAwait(false);

            sw.Stop();
            var latencyMs = sw.Elapsed.TotalMilliseconds;

            var data = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["latency_ms"] = (long)latencyMs,
                ["row_sample"] = count,
            };

            if (latencyMs >= 1_000)
            {
                return HealthCheckResult.Degraded(
                    $"live_game_sessions reachable but slow ({latencyMs:F0} ms ≥ 1000 ms threshold)",
                    data: data);
            }

            return HealthCheckResult.Healthy(
                $"live_game_sessions reachable ({latencyMs:F0} ms)",
                data: data);
        }
#pragma warning disable CA1031 // Health check must never propagate exceptions
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "LiveSessionPersistenceHealthCheck failed (latency {LatencyMs:F0} ms)", sw.Elapsed.TotalMilliseconds);
            return HealthCheckResult.Unhealthy(
                "live_game_sessions table unreachable",
                exception: ex);
        }
#pragma warning restore CA1031
    }
}
