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

        // Disable email for the specific notification type (GDPR: granular opt-out)
        switch (command.NotificationType)
        {
            case "pdf_upload_completed":
            case "document_ready":
                preferences.UpdateEmailPreferences(
                    onReady: false,
                    onFailed: preferences.EmailOnDocumentFailed,
                    onRetry: preferences.EmailOnRetryAvailable);
                break;

            case "processing_failed":
            case "document_failed":
                preferences.UpdateEmailPreferences(
                    onReady: preferences.EmailOnDocumentReady,
                    onFailed: false,
                    onRetry: preferences.EmailOnRetryAvailable);
                break;

            case "retry_available":
                preferences.UpdateEmailPreferences(
                    onReady: preferences.EmailOnDocumentReady,
                    onFailed: preferences.EmailOnDocumentFailed,
                    onRetry: false);
                break;

            case "game_night_invitation":
                preferences.UpdateGameNightPreferences(
                    inAppOnInvitation: preferences.InAppOnGameNightInvitation,
                    emailOnInvitation: false,
                    pushOnInvitation: preferences.PushOnGameNightInvitation,
                    emailOnReminder: preferences.EmailOnGameNightReminder,
                    pushOnReminder: preferences.PushOnGameNightReminder);
                break;

            case "game_night_reminder_24h":
            case "game_night_reminder_1h":
            case "game_night_reminder":
                preferences.UpdateGameNightPreferences(
                    inAppOnInvitation: preferences.InAppOnGameNightInvitation,
                    emailOnInvitation: preferences.EmailOnGameNightInvitation,
                    pushOnInvitation: preferences.PushOnGameNightInvitation,
                    emailOnReminder: false,
                    pushOnReminder: preferences.PushOnGameNightReminder);
                break;

            default:
                // "all" or unknown type: disable all email preferences
                preferences.UpdateEmailPreferences(
                    onReady: false,
                    onFailed: false,
                    onRetry: false);
                preferences.UpdateGameNightPreferences(
                    inAppOnInvitation: preferences.InAppOnGameNightInvitation,
                    emailOnInvitation: false,
                    pushOnInvitation: preferences.PushOnGameNightInvitation,
                    emailOnReminder: false,
                    pushOnReminder: preferences.PushOnGameNightReminder);
                break;
        }

        await _preferencesRepository.UpdateAsync(preferences, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Unsubscribe: User {UserId} unsubscribed from email notifications (type: {NotificationType}, source: {Source})",
            command.UserId, command.NotificationType, command.Source);

        return true;
    }
}
