using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of IRagExecutionRepository.
/// Issue #4458: RAG Execution History
/// </summary>
internal sealed class RagExecutionRepository : RepositoryBase, IRagExecutionRepository
{

    public RagExecutionRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task AddAsync(RagExecution execution, CancellationToken cancellationToken = default)
    {
        await DbContext.Set<RagExecution>().AddAsync(execution, cancellationToken).ConfigureAwait(false);
        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<RagExecution?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<RagExecution>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<(List<RagExecution> Items, int TotalCount)> GetPagedAsync(
        int skip,
        int take,
        string? strategy = null,
        string? status = null,
        int? minLatencyMs = null,
        int? maxLatencyMs = null,
        double? minConfidence = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        CancellationToken cancellationToken = default)
    {
        var query = DbContext.Set<RagExecution>().AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(strategy))
            query = query.Where(e => e.Strategy == strategy);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(e => e.Status == status);

        if (minLatencyMs.HasValue)
            query = query.Where(e => e.TotalLatencyMs >= minLatencyMs.Value);

        if (maxLatencyMs.HasValue)
            query = query.Where(e => e.TotalLatencyMs <= maxLatencyMs.Value);

        if (minConfidence.HasValue)
            query = query.Where(e => e.Confidence != null && e.Confidence >= minConfidence.Value);

        if (dateFrom.HasValue)
            query = query.Where(e => e.CreatedAt >= dateFrom.Value);

        if (dateTo.HasValue)
            query = query.Where(e => e.CreatedAt <= dateTo.Value);

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var items = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (items, totalCount);
    }

    public async Task<RagExecutionStats> GetStatsAsync(
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        CancellationToken cancellationToken = default)
    {
        var query = DbContext.Set<RagExecution>().AsNoTracking().AsQueryable();

        if (dateFrom.HasValue)
            query = query.Where(e => e.CreatedAt >= dateFrom.Value);

        if (dateTo.HasValue)
            query = query.Where(e => e.CreatedAt <= dateTo.Value);

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        if (totalCount == 0)
            return new RagExecutionStats(0, 0, 0, 0, 0, 0);

        var avgLatency = await query.AverageAsync(e => e.TotalLatencyMs, cancellationToken).ConfigureAwait(false);
        var errorCount = await query.CountAsync(e => e.Status == "Error", cancellationToken).ConfigureAwait(false);
        var cacheHitCount = await query.CountAsync(e => e.CacheHit, cancellationToken).ConfigureAwait(false);
        var totalCost = await query.SumAsync(e => e.TotalCost, cancellationToken).ConfigureAwait(false);
        var avgConfidence = await query
            .Where(e => e.Confidence != null)
            .Select(e => e.Confidence!.Value)
            .DefaultIfEmpty(0)
            .AverageAsync(cancellationToken)
            .ConfigureAwait(false);

        return new RagExecutionStats(
            TotalExecutions: totalCount,
            AvgLatencyMs: avgLatency,
            ErrorRate: totalCount > 0 ? (double)errorCount / totalCount : 0,
            CacheHitRate: totalCount > 0 ? (double)cacheHitCount / totalCount : 0,
            TotalCost: totalCost,
            AvgConfidence: avgConfidence);
    }

    public async Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<RagExecution>()
            .Where(e => e.CreatedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<List<AgentExecutionStats>> GetStatsByAgentAsync(
        DateTime dateFrom,
        DateTime dateTo,
        Guid? agentDefinitionId = null,
        CancellationToken cancellationToken = default)
    {
        var query = DbContext.Set<RagExecution>()
            .AsNoTracking()
            .Where(e => e.AgentDefinitionId != null)
            .Where(e => e.CreatedAt >= dateFrom && e.CreatedAt <= dateTo);

        if (agentDefinitionId.HasValue)
            query = query.Where(e => e.AgentDefinitionId == agentDefinitionId.Value);

        var rawResult = await query
            .GroupBy(e => new { e.AgentDefinitionId, e.AgentName, e.Model, e.Provider })
            .Select(g => new
            {
                g.Key.AgentDefinitionId,
                g.Key.AgentName,
                g.Key.Model,
                g.Key.Provider,
                ExecutionCount = g.Count(),
                TotalTokens = g.Sum(e => e.TotalTokens),
                AvgTokens = g.Average(e => (double)e.TotalTokens),
                TotalCost = g.Sum(e => e.TotalCost),
                SuccessCount = g.Count(e => e.Status == "Success"),
                AvgLatencyMs = g.Average(e => (double)e.TotalLatencyMs),
                AvgConfidence = g.Where(e => e.Confidence != null)
                    .Select(e => e.Confidence!.Value)
                    .DefaultIfEmpty(0)
                    .Average(),
                LastExecutedAt = g.Max(e => (DateTime?)e.CreatedAt)
            })
            .OrderByDescending(s => s.ExecutionCount)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rawResult.Select(r => new AgentExecutionStats(
            r.AgentDefinitionId!.Value,
            r.AgentName,
            r.Model,
            r.Provider,
            r.ExecutionCount,
            r.TotalTokens,
            r.AvgTokens,
            r.TotalCost,
            r.ExecutionCount > 0 ? (double)r.SuccessCount / r.ExecutionCount : 0,
            r.AvgLatencyMs,
            r.AvgConfidence,
            r.LastExecutedAt)).ToList();
    }

    /// <inheritdoc />
    public async Task<List<AgentTimeSeriesPoint>> GetTimeSeriesByAgentAsync(
        Guid agentDefinitionId,
        DateTime dateFrom,
        DateTime dateTo,
        CancellationToken cancellationToken = default)
    {
        var rawResult = await DbContext.Set<RagExecution>()
            .AsNoTracking()
            .Where(e => e.AgentDefinitionId == agentDefinitionId)
            .Where(e => e.CreatedAt >= dateFrom && e.CreatedAt <= dateTo)
            .GroupBy(e => e.CreatedAt.Date)
            .Select(g => new
            {
                Date = g.Key,
                Executions = g.Count(),
                TotalTokens = g.Sum(e => e.TotalTokens),
                Cost = g.Sum(e => e.TotalCost),
                AvgLatencyMs = g.Average(e => (double)e.TotalLatencyMs),
                SuccessCount = g.Count(e => e.Status == "Success")
            })
            .OrderBy(p => p.Date)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rawResult.Select(r => new AgentTimeSeriesPoint(
            r.Date,
            r.Executions,
            r.TotalTokens,
            r.Cost,
            r.AvgLatencyMs,
            r.Executions > 0 ? (double)r.SuccessCount / r.Executions : 0)).ToList();
    }

    /// <inheritdoc />
    public async Task<Dictionary<Guid, List<AgentTimeSeriesPoint>>> GetTimeSeriesByAgentsAsync(
        List<Guid> agentDefinitionIds,
        DateTime dateFrom,
        DateTime dateTo,
        CancellationToken cancellationToken = default)
    {
        if (agentDefinitionIds.Count == 0)
            return new Dictionary<Guid, List<AgentTimeSeriesPoint>>();

        var rawResult = await DbContext.Set<RagExecution>()
            .AsNoTracking()
            .Where(e => e.AgentDefinitionId != null && agentDefinitionIds.Contains(e.AgentDefinitionId.Value))
            .Where(e => e.CreatedAt >= dateFrom && e.CreatedAt <= dateTo)
            .GroupBy(e => new { e.AgentDefinitionId, Date = e.CreatedAt.Date })
            .Select(g => new
            {
                AgentId = g.Key.AgentDefinitionId!.Value,
                g.Key.Date,
                Executions = g.Count(),
                TotalTokens = g.Sum(e => e.TotalTokens),
                Cost = g.Sum(e => e.TotalCost),
                AvgLatencyMs = g.Average(e => (double)e.TotalLatencyMs),
                SuccessCount = g.Count(e => e.Status == "Success")
            })
            .OrderBy(p => p.Date)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rawResult
            .GroupBy(r => r.AgentId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(r => new AgentTimeSeriesPoint(
                    r.Date,
                    r.Executions,
                    r.TotalTokens,
                    r.Cost,
                    r.AvgLatencyMs,
                    r.Executions > 0 ? (double)r.SuccessCount / r.Executions : 0)).ToList());
    }
}
