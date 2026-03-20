using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.UserNotifications.Application.Commands;

/// <summary>
/// Handles push notification subscription removal.
/// Issue #4416: Push notifications via Service Worker.
/// </summary>
internal class UnsubscribePushNotificationsCommandHandler : ICommandHandler<UnsubscribePushNotificationsCommand>
{
    private readonly INotificationPreferencesRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UnsubscribePushNotificationsCommandHandler> _logger;

    public UnsubscribePushNotificationsCommandHandler(
        INotificationPreferencesRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<UnsubscribePushNotificationsCommandHandler> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Handle(UnsubscribePushNotificationsCommand command, CancellationToken cancellationToken)
    {
        var prefs = await _repository.GetByUserIdAsync(command.UserId, cancellationToken).ConfigureAwait(false);

        if (prefs == null)
        {
            _logger.LogWarning("No notification preferences found for user {UserId} during unsubscribe", command.UserId);
            return;
        }

        prefs.ClearPushSubscription();
        await _repository.UpdateAsync(prefs, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Push subscription removed for user {UserId}", command.UserId);
    }
}
