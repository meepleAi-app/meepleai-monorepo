using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Infrastructure.Repositories;

internal sealed class ServiceCallLogRepository : IServiceCallLogRepository
{
    private readonly MeepleAiDbContext _db;

    public ServiceCallLogRepository(MeepleAiDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task AddAsync(ServiceCallLogEntry entry, CancellationToken ct)
    {
        _db.ServiceCallLogs.Add(entry);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<(IReadOnlyList<ServiceCallLogEntry> Items, int TotalCount)> GetPagedAsync(
        string? serviceName, bool? isSuccess, string? correlationId,
        DateTime? from, DateTime? toUtc, long? minLatencyMs,
        int page, int pageSize, CancellationToken ct)
    {
        var query = _db.ServiceCallLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(serviceName))
            query = query.Where(x => x.ServiceName == serviceName);
        if (isSuccess.HasValue)
            query = query.Where(x => x.IsSuccess == isSuccess.Value);
        if (!string.IsNullOrWhiteSpace(correlationId))
            query = query.Where(x => x.CorrelationId == correlationId);
        if (from.HasValue)
            query = query.Where(x => x.TimestampUtc >= from.Value);
        if (toUtc.HasValue)
            query = query.Where(x => x.TimestampUtc <= toUtc.Value);
        if (minLatencyMs.HasValue)
            query = query.Where(x => x.LatencyMs >= minLatencyMs.Value);

        var totalCount = await query.CountAsync(ct).ConfigureAwait(false);
        var items = await query
            .OrderByDescending(x => x.TimestampUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return (items, totalCount);
    }

    public async Task DeleteOlderThanAsync(DateTime cutoff, CancellationToken ct)
    {
        await _db.ServiceCallLogs
            .Where(x => x.TimestampUtc < cutoff)
            .ExecuteDeleteAsync(ct)
            .ConfigureAwait(false);
    }
}
