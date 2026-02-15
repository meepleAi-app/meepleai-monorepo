using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of IRagExecutionRepository.
/// Issue #4458: RAG Execution History
/// </summary>
internal sealed class RagExecutionRepository : IRagExecutionRepository
{
    private readonly MeepleAiDbContext _context;

    public RagExecutionRepository(MeepleAiDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task AddAsync(RagExecution execution, CancellationToken cancellationToken = default)
    {
        await _context.Set<RagExecution>().AddAsync(execution, cancellationToken).ConfigureAwait(false);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<RagExecution?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Set<RagExecution>()
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
        var query = _context.Set<RagExecution>().AsNoTracking().AsQueryable();

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
        var query = _context.Set<RagExecution>().AsNoTracking().AsQueryable();

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
        return await _context.Set<RagExecution>()
            .Where(e => e.CreatedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
