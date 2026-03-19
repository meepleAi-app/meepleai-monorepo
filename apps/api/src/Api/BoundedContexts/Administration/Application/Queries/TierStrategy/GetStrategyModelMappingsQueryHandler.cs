using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Queries.TierStrategy;
using Api.BoundedContexts.KnowledgeBase.Domain.Configuration;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Queries.TierStrategy;

/// <summary>
/// Handler for GetStrategyModelMappingsQuery.
/// Returns all strategy-to-model mappings for admin configuration.
/// Issue #3440: Admin UI for tier-strategy configuration.
/// </summary>
internal class GetStrategyModelMappingsQueryHandler
    : IQueryHandler<GetStrategyModelMappingsQuery, IReadOnlyList<StrategyModelMappingDto>>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GetStrategyModelMappingsQueryHandler> _logger;

    public GetStrategyModelMappingsQueryHandler(
        MeepleAiDbContext dbContext,
        ILogger<GetStrategyModelMappingsQueryHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<StrategyModelMappingDto>> Handle(
        GetStrategyModelMappingsQuery request,
        CancellationToken cancellationToken)
    {
        // Get database entries
        var dbEntries = await _dbContext.Set<StrategyModelMappingEntity>()
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var mappings = new List<StrategyModelMappingDto>();

        // Build mappings for all strategies
        foreach (var strategy in Enum.GetValues<RagStrategy>())
        {
            var strategyName = strategy.GetDisplayName();
            var dbEntry = dbEntries.FirstOrDefault(e => string.Equals(e.Strategy, strategyName, StringComparison.Ordinal));

            if (dbEntry != null)
            {
                mappings.Add(new StrategyModelMappingDto(
                    Id: dbEntry.Id,
                    Strategy: strategyName,
                    Provider: dbEntry.Provider,
                    PrimaryModel: dbEntry.PrimaryModel,
                    FallbackModels: dbEntry.FallbackModels,
                    IsCustomizable: dbEntry.IsCustomizable,
                    AdminOnly: dbEntry.AdminOnly,
                    IsDefault: false));
            }
            else
            {
                // Use defaults from configuration
                var defaultConfig = DefaultStrategyModelMappings.GetMapping(strategy);
                mappings.Add(new StrategyModelMappingDto(
                    Id: null,
                    Strategy: strategyName,
                    Provider: defaultConfig.Provider,
                    PrimaryModel: defaultConfig.PrimaryModel,
                    FallbackModels: defaultConfig.FallbackModels,
                    IsCustomizable: defaultConfig.IsCustomizable,
                    AdminOnly: strategy.RequiresAdmin(),
                    IsDefault: true));
            }
        }

        _logger.LogDebug(
            "Retrieved {Count} strategy-model mappings ({DbCount} from database, {DefaultCount} defaults)",
            mappings.Count,
            dbEntries.Count,
            mappings.Count - dbEntries.Count);

        return mappings;
    }
}
