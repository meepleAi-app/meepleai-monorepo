using Api.BoundedContexts.UserNotifications.Application.DTOs;
using Api.BoundedContexts.UserNotifications.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities.UserNotifications;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.UserNotifications.Application.Queries;

/// <summary>
/// Handler for GetAdminEmailHistoryQuery.
/// Returns paginated email history with optional search.
/// Issue #39: Admin email management.
/// </summary>
internal class GetAdminEmailHistoryQueryHandler : IQueryHandler<GetAdminEmailHistoryQuery, AdminEmailHistoryResult>
{
    private readonly MeepleAiDbContext _dbContext;

    public GetAdminEmailHistoryQueryHandler(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AdminEmailHistoryResult> Handle(GetAdminEmailHistoryQuery query, CancellationToken cancellationToken)
    {
        var dbQuery = _dbContext.Set<EmailQueueEntity>().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // EF.Functions.ILike is PostgreSQL case-insensitive LIKE
            var searchPattern = $"%{query.Search}%";
            dbQuery = dbQuery.Where(e =>
                EF.Functions.ILike(e.To, searchPattern) ||
                EF.Functions.ILike(e.Subject, searchPattern));
        }

        var totalCount = await dbQuery.CountAsync(cancellationToken).ConfigureAwait(false);

        var items = await dbQuery
            .OrderByDescending(e => e.CreatedAt)
            .Skip(query.Skip)
            .Take(query.Take)
            .Select(e => new EmailQueueItemDto(
                e.Id, e.UserId, e.To, e.Subject, e.Status,
                e.RetryCount, e.MaxRetries, e.ErrorMessage,
                e.CreatedAt, e.ProcessedAt, e.FailedAt, e.CorrelationId))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return new AdminEmailHistoryResult(items, totalCount, query.Skip, query.Take);
    }
}
