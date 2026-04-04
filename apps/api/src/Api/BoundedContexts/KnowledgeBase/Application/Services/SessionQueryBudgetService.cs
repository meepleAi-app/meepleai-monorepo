using Api.SharedKernel.Services;
using StackExchange.Redis;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Tracks per-session AI query usage and enforces the tier-based session query budget.
/// E4-3: Session Query Budget.
/// </summary>
public interface ISessionQueryBudgetService
{
    /// <summary>
    /// Checks if the session still has query budget remaining.
    /// </summary>
    Task<bool> CanQueryAsync(Guid sessionId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Records a query against the session's budget.
    /// </summary>
    Task RecordQueryAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>
    /// Gets current query count for a session.
    /// </summary>
    Task<SessionQueryUsage> GetUsageAsync(Guid sessionId, Guid userId, CancellationToken ct = default);
}

/// <summary>
/// Snapshot of session query budget usage.
/// </summary>
public sealed record SessionQueryUsage(int QueriesUsed, int QueriesMax, bool CanQuery);

internal sealed class SessionQueryBudgetService : ISessionQueryBudgetService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ITierEnforcementService _tierService;
    private readonly ILogger<SessionQueryBudgetService> _logger;

    // Key format: session:queries:{sessionId}
    // TTL: 24 hours (sessions are ephemeral)
    private const string KeyPrefix = "session:queries:";
    private static readonly TimeSpan KeyTtl = TimeSpan.FromHours(24);

    public SessionQueryBudgetService(
        IConnectionMultiplexer redis,
        ITierEnforcementService tierService,
        ILogger<SessionQueryBudgetService> logger)
    {
        _redis = redis;
        _tierService = tierService;
        _logger = logger;
    }

    public async Task<bool> CanQueryAsync(Guid sessionId, Guid userId, CancellationToken ct = default)
    {
        var limits = await _tierService.GetLimitsAsync(userId, ct).ConfigureAwait(false);
        var db = _redis.GetDatabase();
        var key = $"{KeyPrefix}{sessionId}";
        var raw = await db.StringGetAsync(key).ConfigureAwait(false);
        var current = raw.HasValue && int.TryParse(raw.ToString(), System.Globalization.CultureInfo.InvariantCulture, out var val) ? val : 0;

        return current < limits.MaxSessionQueries;
    }

    public async Task RecordQueryAsync(Guid sessionId, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var key = $"{KeyPrefix}{sessionId}";
        var newValue = await db.StringIncrementAsync(key).ConfigureAwait(false);

        // Set TTL on first increment
        if (newValue == 1)
        {
            await db.KeyExpireAsync(key, KeyTtl).ConfigureAwait(false);
        }

        _logger.LogDebug("Session {SessionId} query count: {Count}", sessionId, newValue);
    }

    public async Task<SessionQueryUsage> GetUsageAsync(Guid sessionId, Guid userId, CancellationToken ct = default)
    {
        var limits = await _tierService.GetLimitsAsync(userId, ct).ConfigureAwait(false);
        var db = _redis.GetDatabase();
        var key = $"{KeyPrefix}{sessionId}";
        var raw = await db.StringGetAsync(key).ConfigureAwait(false);
        var current = raw.HasValue && int.TryParse(raw.ToString(), System.Globalization.CultureInfo.InvariantCulture, out var val) ? val : 0;

        return new SessionQueryUsage(
            QueriesUsed: current,
            QueriesMax: limits.MaxSessionQueries,
            CanQuery: current < limits.MaxSessionQueries);
    }
}
