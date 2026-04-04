using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Queries.TierStrategy;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.SystemConfiguration.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Queries.TierStrategy;

/// <summary>
/// Handler for GetTierStrategyMatrixQuery.
/// Returns the complete tier-strategy access matrix for admin configuration.
/// Issue #3440: Admin UI for tier-strategy configuration.
/// </summary>
internal class GetTierStrategyMatrixQueryHandler : IQueryHandler<GetTierStrategyMatrixQuery, TierStrategyMatrixDto>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GetTierStrategyMatrixQueryHandler> _logger;

    public GetTierStrategyMatrixQueryHandler(
        MeepleAiDbContext dbContext,
        ILogger<GetTierStrategyMatrixQueryHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TierStrategyMatrixDto> Handle(
        GetTierStrategyMatrixQuery request,
        CancellationToken cancellationToken)
    {
        // Get all tiers
        var tiers = Enum.GetNames<LlmUserTier>().ToList();

        // Get all strategies with their info
        var strategies = Enum.GetValues<RagStrategy>()
            .Select(s => new StrategyInfoDto(
                Name: s.GetDisplayName(),
                DisplayName: s.GetDisplayName(),
                Description: GetStrategyDescription(s),
                ComplexityLevel: s.GetComplexityLevel(),
                RequiresAdmin: s.RequiresAdmin()))
            .ToList();

        // Get database entries
        var dbEntries = await _dbContext.Set<TierStrategyAccessEntity>()
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Build access matrix with defaults for missing combinations
        var accessMatrix = new List<TierStrategyAccessDto>();

        foreach (var tier in Enum.GetValues<LlmUserTier>())
        {
            foreach (var strategy in Enum.GetValues<RagStrategy>())
            {
                var tierName = tier.ToString();
                var strategyName = strategy.GetDisplayName();

                var dbEntry = dbEntries.FirstOrDefault(
                    e => string.Equals(e.Tier, tierName, StringComparison.Ordinal)
                      && string.Equals(e.Strategy, strategyName, StringComparison.Ordinal));

                if (dbEntry != null)
                {
                    accessMatrix.Add(new TierStrategyAccessDto(
                        Id: dbEntry.Id,
                        Tier: tierName,
                        Strategy: strategyName,
                        IsEnabled: dbEntry.IsEnabled,
                        IsDefault: false));
                }
                else
                {
                    // Use defaults based on tier-strategy rules
                    var isEnabledByDefault = GetDefaultAccess(tier, strategy);
                    accessMatrix.Add(new TierStrategyAccessDto(
                        Id: null,
                        Tier: tierName,
                        Strategy: strategyName,
                        IsEnabled: isEnabledByDefault,
                        IsDefault: true));
                }
            }
        }

        _logger.LogDebug(
            "Retrieved tier-strategy matrix: {TierCount} tiers, {StrategyCount} strategies, {EntryCount} entries",
            tiers.Count,
            strategies.Count,
            accessMatrix.Count);

        return new TierStrategyMatrixDto(
            Tiers: tiers,
            Strategies: strategies,
            AccessMatrix: accessMatrix);
    }

    /// <summary>
    /// Gets the default access for a tier-strategy combination.
    /// Based on the tier hierarchy:
    /// - Anonymous: No access
    /// - User: FAST, BALANCED only
    /// - Editor: FAST, BALANCED, PRECISE
    /// - Admin: All strategies
    /// </summary>
    private static bool GetDefaultAccess(LlmUserTier tier, RagStrategy strategy)
    {
        return tier switch
        {
            LlmUserTier.Anonymous => false,
            LlmUserTier.User => strategy is RagStrategy.Fast or RagStrategy.Balanced,
            LlmUserTier.Editor => strategy is RagStrategy.Fast or RagStrategy.Balanced or RagStrategy.Precise,
            LlmUserTier.Admin => true,
            _ => false
        };
    }

    private static string GetStrategyDescription(RagStrategy strategy)
    {
        return strategy switch
        {
            RagStrategy.Fast => "Quick lookups with minimal processing. Best for simple Q&A.",
            RagStrategy.Balanced => "Standard queries with CRAG evaluation. Default for gameplay questions.",
            RagStrategy.Precise => "High-precision multi-agent validation. Complex rules interpretation.",
            RagStrategy.Expert => "Expert mode with web search and multi-hop reasoning. Research tasks.",
            RagStrategy.Consensus => "Multi-model consensus voting. Critical decisions.",
            RagStrategy.Custom => "Admin-configurable custom strategy. Specialized workflows.",
            _ => "Unknown strategy."
        };
    }
}
