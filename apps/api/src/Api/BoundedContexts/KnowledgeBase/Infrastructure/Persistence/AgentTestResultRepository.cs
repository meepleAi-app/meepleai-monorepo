using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of AgentTestResult repository.
/// Issue #3379: Agent Test Results History &amp; Persistence.
/// </summary>
internal class AgentTestResultRepository : RepositoryBase, IAgentTestResultRepository
{
    public AgentTestResultRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<AgentTestResult?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Set<AgentTestResultEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken).ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<List<AgentTestResult>> GetByTypologyIdAsync(
        Guid typologyId,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<AgentTestResultEntity>()
            .AsNoTracking()
            .Where(r => r.TypologyId == typologyId)
            .OrderByDescending(r => r.ExecutedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<List<AgentTestResult>> GetByExecutedByAsync(
        Guid userId,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<AgentTestResultEntity>()
            .AsNoTracking()
            .Where(r => r.ExecutedBy == userId)
            .OrderByDescending(r => r.ExecutedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<List<AgentTestResult>> GetByDateRangeAsync(
        DateTime from,
        DateTime to,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<AgentTestResultEntity>()
            .AsNoTracking()
            .Where(r => r.ExecutedAt >= from && r.ExecutedAt <= to)
            .OrderByDescending(r => r.ExecutedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<List<AgentTestResult>> GetSavedAsync(
        Guid? userId = null,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var query = DbContext.Set<AgentTestResultEntity>()
            .AsNoTracking()
            .Where(r => r.IsSaved);

        if (userId.HasValue)
        {
            query = query.Where(r => r.ExecutedBy == userId.Value);
        }

        var entities = await query
            .OrderByDescending(r => r.ExecutedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<int> GetCountByTypologyIdAsync(Guid typologyId, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<AgentTestResultEntity>()
            .CountAsync(r => r.TypologyId == typologyId, cancellationToken).ConfigureAwait(false);
    }

    public async Task AddAsync(AgentTestResult testResult, CancellationToken cancellationToken = default)
    {
        CollectDomainEvents(testResult);
        var entity = MapToEntity(testResult);
        await DbContext.Set<AgentTestResultEntity>().AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(AgentTestResult testResult, CancellationToken cancellationToken = default)
    {
        CollectDomainEvents(testResult);
        var entity = MapToEntity(testResult);

        // Detach existing tracked entity to avoid conflicts
        var tracked = DbContext.ChangeTracker.Entries<AgentTestResultEntity>()
            .FirstOrDefault(e => e.Entity.Id == entity.Id);

        if (tracked != null)
            tracked.State = EntityState.Detached;

        DbContext.Set<AgentTestResultEntity>().Update(entity);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Set<AgentTestResultEntity>()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken).ConfigureAwait(false);

        if (entity != null)
        {
            DbContext.Set<AgentTestResultEntity>().Remove(entity);
        }
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<AgentTestResultEntity>()
            .AnyAsync(r => r.Id == id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<AgentTestResult>> GetForMetricsAsync(
        DateTime? from = null,
        DateTime? to = null,
        Guid? typologyId = null,
        string? strategy = null,
        CancellationToken cancellationToken = default)
    {
        var query = DbContext.Set<AgentTestResultEntity>().AsNoTracking();

        if (from.HasValue)
            query = query.Where(r => r.ExecutedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(r => r.ExecutedAt <= to.Value);

        if (typologyId.HasValue)
            query = query.Where(r => r.TypologyId == typologyId.Value);

        if (!string.IsNullOrWhiteSpace(strategy))
            query = query.Where(r => r.StrategyOverride == strategy);

        var entities = await query
            .OrderByDescending(r => r.ExecutedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<(int TotalCount, int TotalTokens, decimal TotalCost, double AvgLatency, double AvgConfidence)> GetAggregateMetricsAsync(
        DateTime? from = null,
        DateTime? to = null,
        Guid? typologyId = null,
        CancellationToken cancellationToken = default)
    {
        var query = DbContext.Set<AgentTestResultEntity>().AsNoTracking();

        if (from.HasValue)
            query = query.Where(r => r.ExecutedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(r => r.ExecutedAt <= to.Value);

        if (typologyId.HasValue)
            query = query.Where(r => r.TypologyId == typologyId.Value);

        var results = await query.Select(r => new
        {
            r.TokensUsed,
            r.CostEstimate,
            r.LatencyMs,
            r.ConfidenceScore
        }).ToListAsync(cancellationToken).ConfigureAwait(false);

        if (results.Count == 0)
            return (0, 0, 0m, 0d, 0d);

        return (
            TotalCount: results.Count,
            TotalTokens: results.Sum(r => r.TokensUsed),
            TotalCost: results.Sum(r => r.CostEstimate),
            AvgLatency: results.Average(r => r.LatencyMs),
            AvgConfidence: results.Average(r => r.ConfidenceScore)
        );
    }

    // ========== Mapping Methods ==========

    private static AgentTestResult MapToDomain(AgentTestResultEntity entity)
    {
        var result = AgentTestResult.Create(
            typologyId: entity.TypologyId,
            query: entity.Query,
            response: entity.Response,
            modelUsed: entity.ModelUsed,
            confidenceScore: entity.ConfidenceScore,
            tokensUsed: entity.TokensUsed,
            costEstimate: entity.CostEstimate,
            latencyMs: entity.LatencyMs,
            executedBy: entity.ExecutedBy,
            strategyOverride: entity.StrategyOverride,
            citationsJson: entity.CitationsJson);

        // Override the generated ID with the one from persistence
        var idProp = typeof(AgentTestResult).GetProperty("Id");
        idProp?.SetValue(result, entity.Id);

        // Override timestamp from DB
        var executedAtProp = typeof(AgentTestResult).GetProperty("ExecutedAt");
        executedAtProp?.SetValue(result, entity.ExecutedAt);

        // Set saved status if applicable
        if (entity.IsSaved)
        {
            result.MarkAsSaved();
        }

        // Set notes if present
        if (!string.IsNullOrEmpty(entity.Notes))
        {
            result.AddNotes(entity.Notes);
        }

        return result;
    }

    private static AgentTestResultEntity MapToEntity(AgentTestResult result)
    {
        return new AgentTestResultEntity
        {
            Id = result.Id,
            TypologyId = result.TypologyId,
            StrategyOverride = result.StrategyOverride,
            ModelUsed = result.ModelUsed,
            Query = result.Query,
            Response = result.Response,
            ConfidenceScore = result.ConfidenceScore,
            TokensUsed = result.TokensUsed,
            CostEstimate = result.CostEstimate,
            LatencyMs = result.LatencyMs,
            CitationsJson = result.CitationsJson,
            ExecutedAt = result.ExecutedAt,
            ExecutedBy = result.ExecutedBy,
            Notes = result.Notes,
            IsSaved = result.IsSaved
        };
    }
}
