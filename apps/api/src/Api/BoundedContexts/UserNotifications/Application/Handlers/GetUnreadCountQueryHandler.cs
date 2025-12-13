using Api.BoundedContexts.UserNotifications.Application.Queries;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Handlers;

/// <summary>
/// Handler for GetUnreadCountQuery.
/// Optimized query for notification badge count.
/// </summary>
public class GetUnreadCountQueryHandler : IQueryHandler<GetUnreadCountQuery, int>
{
    private readonly INotificationRepository _notificationRepository;

    public GetUnreadCountQueryHandler(INotificationRepository notificationRepository)
    {
        _notificationRepository = notificationRepository ?? throw new ArgumentNullException(nameof(notificationRepository));
    }

    public async Task<int> Handle(GetUnreadCountQuery query, CancellationToken cancellationToken)
    {
        return await _notificationRepository.GetUnreadCountAsync(query.UserId, cancellationToken).ConfigureAwait(false);
    }
}
