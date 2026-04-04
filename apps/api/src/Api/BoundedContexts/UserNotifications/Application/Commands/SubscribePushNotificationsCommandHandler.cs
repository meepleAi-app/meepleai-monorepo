using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.UserNotifications.Application.Commands;

/// <summary>
/// Handles push notification subscription registration.
/// Issue #4416: Push notifications via Service Worker.
/// </summary>
internal class SubscribePushNotificationsCommandHandler : ICommandHandler<SubscribePushNotificationsCommand>
{
    private readonly INotificationPreferencesRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SubscribePushNotificationsCommandHandler> _logger;

    public SubscribePushNotificationsCommandHandler(
        INotificationPreferencesRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<SubscribePushNotificationsCommandHandler> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Handle(SubscribePushNotificationsCommand command, CancellationToken cancellationToken)
    {
        var prefs = await _repository.GetByUserIdAsync(command.UserId, cancellationToken).ConfigureAwait(false);

        if (prefs == null)
        {
            prefs = new NotificationPreferences(command.UserId);
            prefs.UpdatePushSubscription(command.Endpoint, command.P256dhKey, command.AuthKey);
            await _repository.AddAsync(prefs, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            prefs.UpdatePushSubscription(command.Endpoint, command.P256dhKey, command.AuthKey);
            await _repository.UpdateAsync(prefs, cancellationToken).ConfigureAwait(false);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Push subscription registered for user {UserId}", command.UserId);
    }
}
