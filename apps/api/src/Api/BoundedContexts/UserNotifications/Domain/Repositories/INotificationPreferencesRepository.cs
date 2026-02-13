using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.UserNotifications.Domain.Repositories;

internal interface INotificationPreferencesRepository : IRepository<NotificationPreferences, Guid>
{
    Task<NotificationPreferences?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
}
