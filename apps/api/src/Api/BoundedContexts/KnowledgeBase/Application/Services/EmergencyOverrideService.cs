using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Issue #5476: Redis-backed emergency override service with TTL-based auto-revert.
/// Uses IMemoryCache as L1 cache to avoid Redis round-trip on every LLM request.
/// Falls back gracefully if Redis is unavailable (overrides inactive = safe default).
/// </summary>
internal sealed class EmergencyOverrideService : IEmergencyOverrideService
{
    private const string KeyPrefix = "llm:emergency:";
    private const string ActiveSetKey = "llm:emergency:active_set";

    // L1 cache TTL — short enough to pick up changes, long enough to avoid Redis on every request
    private static readonly TimeSpan L1CacheTtl = TimeSpan.FromSeconds(5);

    private static readonly string[] AllActions =
    [
        "force-ollama-only",
        "reset-circuit-breaker",
        "flush-quota-cache"
    ];

    private readonly IConnectionMultiplexer _redis;
    private readonly IMemoryCache _memoryCache;
    private readonly IMediator _mediator;
    private readonly ILogger<EmergencyOverrideService> _logger;

    public EmergencyOverrideService(
        IConnectionMultiplexer redis,
        IMemoryCache memoryCache,
        IMediator mediator,
        ILogger<EmergencyOverrideService> logger)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ActivateOverrideAsync(
        string action,
        int durationMinutes,
        string reason,
        Guid adminUserId,
        string? targetProvider = null,
        CancellationToken ct = default)
    {
        var key = KeyPrefix + action;
        var ttl = TimeSpan.FromMinutes(durationMinutes);
        var now = DateTime.UtcNow;

        var payload = new OverridePayload(
            Action: action,
            Reason: reason,
            AdminUserId: adminUserId,
            TargetProvider: targetProvider,
            ActivatedAt: now,
            ExpiresAt: now.Add(ttl));

        try
        {
            var db = _redis.GetDatabase();
            var json = JsonSerializer.Serialize(payload);

            // Store override with TTL (idempotent — overwrites if exists)
            await db.StringSetAsync(key, json, ttl).ConfigureAwait(false);

            // Track active overrides in a set (members auto-expire via key TTL check)
            await db.SetAddAsync(ActiveSetKey, action).ConfigureAwait(false);

            // Invalidate L1 cache
            _memoryCache.Remove($"emergency:{action}");

            _logger.LogWarning(
                "Emergency override activated: {Action} for {Duration}min by admin {AdminId}. Reason: {Reason}",
                action, durationMinutes, adminUserId, reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to activate emergency override {Action} in Redis", action);
            throw;
        }

        // Publish audit event (fire-and-forget)
        _ = Task.Run(async () =>
        {
            try
            {
                await _mediator.Publish(new EmergencyOverrideActivatedEvent(
                    action, durationMinutes, reason, adminUserId, targetProvider,
                    IsDeactivation: false, OccurredAt: now), CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish EmergencyOverrideActivatedEvent");
            }
        }, CancellationToken.None);
    }

    public async Task DeactivateOverrideAsync(string action, Guid adminUserId, CancellationToken ct = default)
    {
        var key = KeyPrefix + action;

        try
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(key).ConfigureAwait(false);
            await db.SetRemoveAsync(ActiveSetKey, action).ConfigureAwait(false);

            // Invalidate L1 cache
            _memoryCache.Remove($"emergency:{action}");

            _logger.LogWarning(
                "Emergency override deactivated: {Action} by admin {AdminId}",
                action, adminUserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deactivate emergency override {Action} in Redis", action);
            throw;
        }

        // Publish audit event (fire-and-forget)
        _ = Task.Run(async () =>
        {
            try
            {
                await _mediator.Publish(new EmergencyOverrideActivatedEvent(
                    action, 0, "Manual deactivation", adminUserId, null,
                    IsDeactivation: true, OccurredAt: DateTime.UtcNow), CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish EmergencyOverrideActivatedEvent (deactivation)");
            }
        }, CancellationToken.None);
    }

    public async Task<bool> IsOverrideActiveAsync(string action, CancellationToken ct = default)
    {
        var cacheKey = $"emergency:{action}";

        if (_memoryCache.TryGetValue(cacheKey, out bool cached))
            return cached;

        try
        {
            var db = _redis.GetDatabase();
            var exists = await db.KeyExistsAsync(KeyPrefix + action).ConfigureAwait(false);

            _memoryCache.Set(cacheKey, exists, L1CacheTtl);
            return exists;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis unavailable for emergency override check — defaulting to inactive (safe)");
            return false; // Fail-open: if Redis is down, overrides are inactive
        }
    }

    public Task<bool> IsForceOllamaOnlyAsync(CancellationToken ct = default)
        => IsOverrideActiveAsync("force-ollama-only", ct);

    public async Task<IReadOnlyList<ActiveOverrideInfo>> GetActiveOverridesAsync(CancellationToken ct = default)
    {
        var result = new List<ActiveOverrideInfo>();

        try
        {
            var db = _redis.GetDatabase();

            foreach (var action in AllActions)
            {
                var json = await db.StringGetAsync(KeyPrefix + action).ConfigureAwait(false);
                if (json.IsNullOrEmpty) continue;

                var payload = JsonSerializer.Deserialize<OverridePayload>(json!);
                if (payload == null) continue;

                var remaining = (int)Math.Max(0, (payload.ExpiresAt - DateTime.UtcNow).TotalMinutes);
                result.Add(new ActiveOverrideInfo(
                    payload.Action,
                    payload.Reason,
                    payload.AdminUserId,
                    payload.TargetProvider,
                    payload.ActivatedAt,
                    payload.ExpiresAt,
                    remaining));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve active overrides from Redis");
        }

        return result;
    }

    private sealed record OverridePayload(
        string Action,
        string Reason,
        Guid AdminUserId,
        string? TargetProvider,
        DateTime ActivatedAt,
        DateTime ExpiresAt);
}
