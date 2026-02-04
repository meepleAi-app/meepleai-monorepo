using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of StrategyPattern repository.
/// Issue #3493: PostgreSQL Schema Extensions for Multi-Agent System.
/// </summary>
internal class StrategyPatternRepository : RepositoryBase, IStrategyPatternRepository
{
    public StrategyPatternRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<List<StrategyPattern>> GetByGameAndPhaseAsync(
        Guid gameId,
        string phase,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<StrategyPatternEntity>()
            .AsNoTracking()
            .Where(p => p.GameId == gameId && p.ApplicablePhase == phase)
            .OrderByDescending(p => p.EvaluationScore)
            .Take(limit)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<List<StrategyPattern>> GetTopRatedByGameIdAsync(
        Guid gameId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<StrategyPatternEntity>()
            .AsNoTracking()
            .Where(p => p.GameId == gameId)
            .OrderByDescending(p => p.EvaluationScore)
            .Take(limit)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<int> CountByGameIdAsync(
        Guid gameId,
        CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<StrategyPatternEntity>()
            .CountAsync(p => p.GameId == gameId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<StrategyPattern?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Set<StrategyPatternEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken).ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task AddAsync(StrategyPattern pattern, CancellationToken cancellationToken = default)
    {
        var entity = MapToEntity(pattern);
        await DbContext.Set<StrategyPatternEntity>().AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(StrategyPattern pattern, CancellationToken cancellationToken = default)
    {
        var entity = MapToEntity(pattern);

        // Detach existing tracked entity to avoid conflicts
        var tracked = DbContext.ChangeTracker.Entries<StrategyPatternEntity>()
            .FirstOrDefault(e => e.Entity.Id == entity.Id);

        if (tracked != null)
            tracked.State = EntityState.Detached;

        DbContext.Set<StrategyPatternEntity>().Update(entity);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await DbContext.Set<StrategyPatternEntity>()
            .Where(p => p.Id == id)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
    }

    // ========== Mapping Methods ==========

    private static StrategyPattern MapToDomain(StrategyPatternEntity entity)
    {
        Vector? embedding = null;
        if (entity.Embedding != null)
        {
            embedding = new Vector(entity.Embedding);
        }

        return new StrategyPattern(
            id: entity.Id,
            gameId: entity.GameId,
            patternName: entity.PatternName,
            applicablePhase: entity.ApplicablePhase,
            description: entity.Description,
            evaluationScore: entity.EvaluationScore,
            boardConditionsJson: entity.BoardConditionsJson,
            moveSequenceJson: entity.MoveSequenceJson,
            source: entity.Source,
            embedding: embedding);
    }

    private static StrategyPatternEntity MapToEntity(StrategyPattern pattern)
    {
        return new StrategyPatternEntity
        {
            Id = pattern.Id,
            GameId = pattern.GameId,
            PatternName = pattern.PatternName,
            ApplicablePhase = pattern.ApplicablePhase,
            Description = pattern.Description,
            EvaluationScore = pattern.EvaluationScore,
            BoardConditionsJson = pattern.BoardConditionsJson,
            MoveSequenceJson = pattern.MoveSequenceJson,
            Source = pattern.Source,
            Embedding = pattern.Embedding?.Values
        };
    }
}
