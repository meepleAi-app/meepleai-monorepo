using Api.BoundedContexts.Administration.Domain.Entities;

namespace Api.BoundedContexts.Administration.Domain.Repositories;

public interface IServiceCallLogRepository
{
    Task AddAsync(ServiceCallLogEntry entry, CancellationToken ct);

    Task<(IReadOnlyList<ServiceCallLogEntry> Items, int TotalCount)> GetPagedAsync(
        string? serviceName, bool? isSuccess, string? correlationId,
        DateTime? from, DateTime? toUtc, long? minLatencyMs,
        int page, int pageSize, CancellationToken ct);

    Task DeleteOlderThanAsync(DateTime cutoff, CancellationToken ct);
}
