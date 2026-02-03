using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities.KnowledgeBase;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

/// <summary>
/// Repository for tier-strategy access records.
/// Issue #3436: Part of tier-strategy-model architecture.
/// </summary>
internal sealed class TierStrategyAccessRepository : ITierStrategyAccessRepository
{
    private const string WildcardStrategy = "*";
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<TierStrategyAccessRepository> _logger;

    public TierStrategyAccessRepository(MeepleAiDbContext dbContext, ILogger<TierStrategyAccessRepository> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetEnabledStrategiesForTierAsync(
        LlmUserTier tier,
        CancellationToken cancellationToken = default)
    {
        var tierName = tier.ToString();

        var strategies = await _dbContext.Set<TierStrategyAccessEntity>()
            .AsNoTracking()
            .Where(e => e.Tier == tierName && e.IsEnabled)
            .Select(e => e.Strategy)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogDebug(
            "Retrieved {Count} enabled strategies for tier {Tier}",
            strategies.Count,
            tierName);

        return strategies;
    }

    /// <inheritdoc/>
    public async Task<bool> IsAccessEnabledAsync(
        LlmUserTier tier,
        string strategy,
        CancellationToken cancellationToken = default)
    {
        var tierName = tier.ToString();

        // Check for direct match or wildcard access
        var hasAccess = await _dbContext.Set<TierStrategyAccessEntity>()
            .AsNoTracking()
            .AnyAsync(
                e => e.Tier == tierName &&
                     e.IsEnabled &&
                     (e.Strategy == strategy || e.Strategy == WildcardStrategy),
                cancellationToken).ConfigureAwait(false);

        _logger.LogDebug(
            "Access check for tier {Tier} to strategy {Strategy}: {HasAccess}",
            tierName,
            strategy,
            hasAccess);

        return hasAccess;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TierStrategyAccessEntry>> GetAllForTierAsync(
        LlmUserTier tier,
        CancellationToken cancellationToken = default)
    {
        var tierName = tier.ToString();

        var entries = await _dbContext.Set<TierStrategyAccessEntity>()
            .AsNoTracking()
            .Where(e => e.Tier == tierName)
            .Select(e => new TierStrategyAccessEntry(e.Tier, e.Strategy, e.IsEnabled))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entries;
    }
}
