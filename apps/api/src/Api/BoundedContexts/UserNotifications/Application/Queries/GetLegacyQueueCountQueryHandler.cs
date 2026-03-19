using Api.BoundedContexts.UserNotifications.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities.UserNotifications;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.UserNotifications.Application.Queries;

/// <summary>
/// Handler for GetLegacyQueueCountQuery.
/// Returns the count of remaining EmailQueueItem rows in the legacy email queue.
/// Used as a migration gate to track progress from email-only to unified notification queue.
/// </summary>
internal class GetLegacyQueueCountQueryHandler : IQueryHandler<GetLegacyQueueCountQuery, int>
{
    private readonly MeepleAiDbContext _dbContext;

    public GetLegacyQueueCountQueryHandler(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<int> Handle(GetLegacyQueueCountQuery query, CancellationToken cancellationToken)
    {
        return await _dbContext.Set<EmailQueueEntity>()
            .AsNoTracking()
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
