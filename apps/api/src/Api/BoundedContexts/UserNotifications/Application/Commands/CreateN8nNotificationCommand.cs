using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Commands;

/// <summary>
/// Command to create a notification triggered by an n8n webhook action.
/// Handles the "send_notification" action from n8n → API webhook callbacks.
/// </summary>
internal record CreateN8nNotificationCommand(
    Guid UserId,
    string Title,
    string Message,
    string Type,
    string? Link
) : ICommand;

/// <summary>
/// Handler for CreateN8nNotificationCommand.
/// Creates an in-app notification for the specified user via the notification repository.
/// </summary>
internal sealed class CreateN8nNotificationCommandHandler : ICommandHandler<CreateN8nNotificationCommand>
{
    private readonly INotificationRepository _notificationRepository;
    private readonly ILogger<CreateN8nNotificationCommandHandler> _logger;

    public CreateN8nNotificationCommandHandler(
        INotificationRepository notificationRepository,
        ILogger<CreateN8nNotificationCommandHandler> logger)
    {
        _notificationRepository = notificationRepository ?? throw new ArgumentNullException(nameof(notificationRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(CreateN8nNotificationCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var notification = new Notification(
            id: Guid.NewGuid(),
            userId: request.UserId,
            type: NotificationType.FromString(request.Type),
            severity: NotificationSeverity.Info,
            title: request.Title,
            message: request.Message,
            link: request.Link
        );

        await _notificationRepository.AddAsync(notification, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "n8n webhook: notification created for user {UserId}, type={Type}",
            request.UserId, request.Type);
    }
}
