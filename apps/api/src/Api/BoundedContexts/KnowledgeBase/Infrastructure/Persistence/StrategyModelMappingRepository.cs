using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

/// <summary>
/// Repository for strategy-to-model mapping records.
/// Issue #3435: Part of tier-strategy-model architecture.
/// </summary>
internal sealed class StrategyModelMappingRepository : RepositoryBase, IStrategyModelMappingRepository
{
    private readonly ILogger<StrategyModelMappingRepository> _logger;

    public StrategyModelMappingRepository(
        MeepleAiDbContext dbContext,
        IDomainEventCollector eventCollector,
        ILogger<StrategyModelMappingRepository> logger)
        : base(dbContext, eventCollector)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<StrategyModelMappingEntry?> GetByStrategyAsync(
        RagStrategy strategy,
        CancellationToken cancellationToken = default)
    {
        var strategyName = strategy.GetDisplayName();

        var entity = await DbContext.Set<StrategyModelMappingEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                e => e.Strategy == strategyName,
                cancellationToken).ConfigureAwait(false);

        if (entity == null)
        {
            _logger.LogDebug(
                "No model mapping found for strategy {Strategy}",
                strategyName);
            return null;
        }

        _logger.LogDebug(
            "Retrieved model mapping for strategy {Strategy}: {Provider}/{Model}",
            strategyName,
            entity.Provider,
            entity.PrimaryModel);

        return new StrategyModelMappingEntry(
            entity.Strategy,
            entity.PrimaryModel,
            entity.FallbackModels,
            entity.Provider,
            entity.IsCustomizable,
            entity.AdminOnly);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<StrategyModelMappingEntry>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var entries = await DbContext.Set<StrategyModelMappingEntity>()
            .AsNoTracking()
            .Select(e => new StrategyModelMappingEntry(
                e.Strategy,
                e.PrimaryModel,
                e.FallbackModels,
                e.Provider,
                e.IsCustomizable,
                e.AdminOnly))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogDebug("Retrieved {Count} strategy-model mappings", entries.Count);

        return entries;
    }

    /// <inheritdoc/>
    public async Task<bool> HasMappingAsync(
        RagStrategy strategy,
        CancellationToken cancellationToken = default)
    {
        var strategyName = strategy.GetDisplayName();

        var exists = await DbContext.Set<StrategyModelMappingEntity>()
            .AsNoTracking()
            .AnyAsync(e => e.Strategy == strategyName, cancellationToken)
            .ConfigureAwait(false);

        return exists;
    }
}
