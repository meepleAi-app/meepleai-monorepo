using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.SharedKernel.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

/// <summary>
/// Repository for tier-strategy access records.
/// Issue #3436: Part of tier-strategy-model architecture.
/// </summary>
internal sealed class TierStrategyAccessRepository : RepositoryBase, ITierStrategyAccessRepository
{
    private const string WildcardStrategy = "*";
    private readonly ILogger<TierStrategyAccessRepository> _logger;

    public TierStrategyAccessRepository(
        MeepleAiDbContext dbContext,
        IDomainEventCollector eventCollector,
        ILogger<TierStrategyAccessRepository> logger)
        : base(dbContext, eventCollector)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetEnabledStrategiesForTierAsync(
        LlmUserTier tier,
        CancellationToken cancellationToken = default)
    {
        var tierName = tier.ToString();

        var strategies = await DbContext.Set<TierStrategyAccessEntity>()
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
        var hasAccess = await DbContext.Set<TierStrategyAccessEntity>()
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

        var entries = await DbContext.Set<TierStrategyAccessEntity>()
            .AsNoTracking()
            .Where(e => e.Tier == tierName)
            .Select(e => new TierStrategyAccessEntry(e.Tier, e.Strategy, e.IsEnabled))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entries;
    }
}
