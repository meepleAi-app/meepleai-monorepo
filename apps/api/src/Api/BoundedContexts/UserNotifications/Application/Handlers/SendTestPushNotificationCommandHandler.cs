using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Handlers;

/// <summary>
/// Sends a test push notification to verify the user's subscription works.
/// Issue #4416: Push notification testing.
/// </summary>
internal class SendTestPushNotificationCommandHandler : ICommandHandler<SendTestPushNotificationCommand>
{
    private readonly INotificationPreferencesRepository _repository;
    private readonly IPushNotificationService _pushService;
    private readonly ILogger<SendTestPushNotificationCommandHandler> _logger;

    public SendTestPushNotificationCommandHandler(
        INotificationPreferencesRepository repository,
        IPushNotificationService pushService,
        ILogger<SendTestPushNotificationCommandHandler> logger)
    {
        _repository = repository;
        _pushService = pushService;
        _logger = logger;
    }

    public async Task Handle(SendTestPushNotificationCommand command, CancellationToken cancellationToken)
    {
        var prefs = await _repository.GetByUserIdAsync(command.UserId, cancellationToken).ConfigureAwait(false);

        if (prefs is null || !prefs.HasPushSubscription)
        {
            _logger.LogWarning("Test push requested but no subscription found for user {UserId}", command.UserId);
            throw new ConflictException("No push subscription found. Please enable push notifications first.");
        }

        await _pushService.SendPushNotificationAsync(
            prefs.PushEndpoint!,
            prefs.PushP256dhKey!,
            prefs.PushAuthKey!,
            "Test Notification",
            "Push notifications are working correctly!",
            "/settings/notifications",
            cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Test push notification sent to user {UserId}", command.UserId);
    }
}
