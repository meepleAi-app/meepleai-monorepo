using Api.BoundedContexts.UserNotifications.Application.Queries;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Queries;

/// <summary>
/// Handler for GetUnreadCountQuery.
/// Optimized query for notification badge count.
/// </summary>
internal class GetUnreadCountQueryHandler : IQueryHandler<GetUnreadCountQuery, int>
{
    private readonly INotificationRepository _notificationRepository;

    public GetUnreadCountQueryHandler(INotificationRepository notificationRepository)
    {
        ArgumentNullException.ThrowIfNull(notificationRepository);
        _notificationRepository = notificationRepository;
    }

    public async Task<int> Handle(GetUnreadCountQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return await _notificationRepository.GetUnreadCountAsync(query.UserId, cancellationToken).ConfigureAwait(false);
    }
}
