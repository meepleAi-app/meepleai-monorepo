using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.UserNotifications.Application.Handlers;

/// <summary>
/// Handler for UnsubscribeEmailCommand.
/// Disables email preference for a specific notification type.
/// Logs the change for audit trail.
/// Issue #38: GDPR-compliant unsubscribe.
/// </summary>
internal class UnsubscribeEmailCommandHandler : ICommandHandler<UnsubscribeEmailCommand, bool>
{
    private readonly INotificationPreferencesRepository _preferencesRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UnsubscribeEmailCommandHandler> _logger;

    public UnsubscribeEmailCommandHandler(
        INotificationPreferencesRepository preferencesRepository,
        IUnitOfWork unitOfWork,
        ILogger<UnsubscribeEmailCommandHandler> logger)
    {
        _preferencesRepository = preferencesRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<bool> Handle(UnsubscribeEmailCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var preferences = await _preferencesRepository
            .GetByUserIdAsync(command.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (preferences == null)
        {
            _logger.LogWarning("Unsubscribe: No preferences found for user {UserId}", command.UserId);
            return false;
        }

        // Disable email for the specific notification type
        // Currently preferences are grouped by PDF processing events
        // For now, disable all email preferences as a simple implementation
        preferences.UpdateEmailPreferences(
            onReady: false,
            onFailed: false,
            onRetry: false);

        await _preferencesRepository.UpdateAsync(preferences, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Unsubscribe: User {UserId} unsubscribed from email notifications (type: {NotificationType}, source: {Source})",
            command.UserId, command.NotificationType, command.Source);

        return true;
    }
}
