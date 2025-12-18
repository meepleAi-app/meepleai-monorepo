using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.Observability;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Api.BoundedContexts.UserNotifications.Application.Handlers;

/// <summary>
/// Handler for MarkNotificationReadCommand.
/// Marks a single notification as read.
/// </summary>
internal class MarkNotificationReadCommandHandler : ICommandHandler<MarkNotificationReadCommand, bool>
{
    private readonly INotificationRepository _notificationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<MarkNotificationReadCommandHandler> _logger;

    public MarkNotificationReadCommandHandler(
        INotificationRepository notificationRepository,
        IUnitOfWork unitOfWork,
        ILogger<MarkNotificationReadCommandHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(notificationRepository);
        _notificationRepository = notificationRepository;
        ArgumentNullException.ThrowIfNull(unitOfWork);
        _unitOfWork = unitOfWork;
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    public async Task<bool> Handle(MarkNotificationReadCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var stopwatch = Stopwatch.StartNew();

        var notification = await _notificationRepository.GetByIdAsync(command.NotificationId, cancellationToken).ConfigureAwait(false);

        if (notification == null)
        {
            _logger.LogWarning("Notification {NotificationId} not found for user {UserId}",
                command.NotificationId, command.UserId);
            return false; // Notification not found
        }

        // Verify ownership
        if (notification.UserId != command.UserId)
        {
            _logger.LogWarning("Unauthorized attempt to mark notification {NotificationId} as read by user {UserId}",
                command.NotificationId, command.UserId);
            throw new UnauthorizedAccessException($"User {command.UserId} cannot mark notification {command.NotificationId} as read");
        }

        notification.MarkAsRead();
        await _notificationRepository.UpdateAsync(notification, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        stopwatch.Stop();
        MeepleAiMetrics.RecordNotificationRead(stopwatch.Elapsed.TotalMilliseconds, notification.Type?.ToString());

        _logger.LogInformation("Marked notification {NotificationId} as read for user {UserId}",
            notification.Id, command.UserId);

        return true;
    }
}
