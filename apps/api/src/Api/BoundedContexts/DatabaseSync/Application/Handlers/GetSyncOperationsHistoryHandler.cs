using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.DatabaseSync.Application.Queries;
using Api.BoundedContexts.DatabaseSync.Domain.Models;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DatabaseSync.Application.Handlers;

internal class GetSyncOperationsHistoryHandler : IQueryHandler<GetSyncOperationsHistoryQuery, IReadOnlyList<SyncOperationEntry>>
{
    private readonly MeepleAiDbContext _dbContext;

    public GetSyncOperationsHistoryHandler(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<IReadOnlyList<SyncOperationEntry>> Handle(
        GetSyncOperationsHistoryQuery query, CancellationToken cancellationToken)
    {
        var auditEntries = await _dbContext.Set<AuditLog>()
            .Where(a => a.Action.StartsWith("DatabaseSync."))
            .OrderByDescending(a => a.CreatedAt)
            .Take(query.Limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var result = new List<SyncOperationEntry>(auditEntries.Count);
        foreach (var entry in auditEntries)
        {
            _ = Guid.TryParse(entry.ResourceId, out var operationId);

            var syncResult = string.Equals(entry.Result, "Success", StringComparison.Ordinal)
                ? new SyncResult(true, 0, 0, operationId)
                : new SyncResult(false, 0, 0, operationId, entry.Details);

            result.Add(new SyncOperationEntry(
                OperationId: operationId,
                OperationType: entry.Action,
                TableName: entry.Resource,
                Result: syncResult,
                AdminUserId: entry.UserId ?? Guid.Empty,
                StartedAt: entry.CreatedAt,
                CompletedAt: entry.CreatedAt));
        }

        return result;
    }
}
