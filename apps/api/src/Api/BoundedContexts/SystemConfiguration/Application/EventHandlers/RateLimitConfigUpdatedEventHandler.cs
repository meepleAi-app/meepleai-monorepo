using Api.BoundedContexts.SystemConfiguration.Domain.Events;
using Api.Infrastructure;
using Api.Services;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SystemConfiguration.Application.EventHandlers;

/// <summary>
/// Handler for RateLimitConfigUpdatedEvent domain event.
/// Automatically creates audit log entry and invalidates tier config cache via base class.
/// </summary>
internal sealed class RateLimitConfigUpdatedEventHandler : DomainEventHandlerBase<RateLimitConfigUpdatedEvent>
{
    private readonly IHybridCacheService _cache;

    public RateLimitConfigUpdatedEventHandler(
        MeepleAiDbContext dbContext,
        IHybridCacheService cache,
        ILogger<RateLimitConfigUpdatedEventHandler> logger)
        : base(dbContext, logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    protected override async Task HandleEventAsync(
        RateLimitConfigUpdatedEvent domainEvent,
        CancellationToken cancellationToken)
    {
        // Invalidate cache for the updated tier configuration
        var cacheKey = $"rate_limit_config_{domainEvent.Tier}";
        await _cache.RemoveAsync(cacheKey, cancellationToken).ConfigureAwait(false);

        Logger.LogInformation(
            "Invalidated cache for rate limit config: {CacheKey}",
            cacheKey);
    }

    protected override Dictionary<string, object?>? GetAuditMetadata(RateLimitConfigUpdatedEvent domainEvent)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ConfigId"] = domainEvent.ConfigId,
            ["Tier"] = domainEvent.Tier.ToString(),
            ["Action"] = "RateLimitConfigUpdated"
        };
    }

    protected override Guid? GetUserId(RateLimitConfigUpdatedEvent domainEvent)
    {
        // AdminId is not tracked in event - would need to be added if audit requires it
        return null;
    }
}
