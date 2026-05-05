using Api.BoundedContexts.UserNotifications.Application.Services;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.BoundedContexts.UserNotifications.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Api.BoundedContexts.UserNotifications.Infrastructure.Services;

/// <summary>
/// Dispatches notifications to multiple channels: in-app, email queue, Slack DM, Slack team.
/// Creates in-app notification synchronously and enqueues async channel deliveries.
/// Best-effort: channel failures are logged but do not propagate.
/// </summary>
internal sealed class NotificationDispatcher : INotificationDispatcher
{
    private readonly INotificationRepository _notificationRepository;
    private readonly INotificationQueueRepository _notificationQueueRepository;
    private readonly INotificationPreferencesRepository _preferencesRepository;
    private readonly ISlackConnectionRepository _slackConnectionRepository;
    private readonly SlackNotificationConfiguration _slackConfig;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(
        INotificationRepository notificationRepository,
        INotificationQueueRepository notificationQueueRepository,
        INotificationPreferencesRepository preferencesRepository,
        ISlackConnectionRepository slackConnectionRepository,
        IOptions<SlackNotificationConfiguration> slackConfig,
        ILogger<NotificationDispatcher> logger)
    {
        _notificationRepository = notificationRepository ?? throw new ArgumentNullException(nameof(notificationRepository));
        _notificationQueueRepository = notificationQueueRepository ?? throw new ArgumentNullException(nameof(notificationQueueRepository));
        _preferencesRepository = preferencesRepository ?? throw new ArgumentNullException(nameof(preferencesRepository));
        _slackConnectionRepository = slackConnectionRepository ?? throw new ArgumentNullException(nameof(slackConnectionRepository));
        _slackConfig = slackConfig?.Value ?? throw new ArgumentNullException(nameof(slackConfig));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task DispatchAsync(NotificationMessage message, CancellationToken ct = default)
    {
        var correlationId = Guid.NewGuid();

        // 1. Always create in-app notification
        var metadataJson = message.Metadata is not null
            ? System.Text.Json.JsonSerializer.Serialize(message.Metadata)
            : null;

        var notification = new Notification(
            id: Guid.NewGuid(),
            userId: message.RecipientUserId,
            type: message.Type,
            severity: ResolveSeverity(message.Type),
            title: ResolveTitle(message.Type),
            message: message.Payload.ToString() ?? string.Empty,
            link: message.DeepLinkPath,
            metadata: metadataJson,
            correlationId: correlationId);

        await _notificationRepository.AddAsync(notification, ct).ConfigureAwait(false);

        _logger.LogInformation(
            "Created in-app notification {NotificationId} for user {UserId}, type={Type}, correlationId={CorrelationId}",
            notification.Id, message.RecipientUserId, message.Type, correlationId);

        // 2. Resolve user preferences (default if none configured)
        var queueItems = new List<NotificationQueueItem>();

        NotificationPreferences? preferences = null;
        try
        {
            preferences = await _preferencesRepository.GetByUserIdAsync(message.RecipientUserId, ct).ConfigureAwait(false);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load notification preferences for user {UserId}, skipping channel dispatch", message.RecipientUserId);
        }
#pragma warning restore CA1031

        // 3. Check Email channel
        try
        {
            if (preferences == null || IsEmailEnabledForType(preferences, message.Type))
            {
                var emailItem = NotificationQueueItem.Create(
                    channelType: NotificationChannelType.Email,
                    recipientUserId: message.RecipientUserId,
                    notificationType: message.Type,
                    payload: message.Payload,
                    correlationId: correlationId,
                    deepLinkPath: message.DeepLinkPath);

                queueItems.Add(emailItem);

                _logger.LogDebug("Enqueuing email notification for user {UserId}, type={Type}", message.RecipientUserId, message.Type);
            }
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve email channel for user {UserId}", message.RecipientUserId);
        }
#pragma warning restore CA1031

        // 4. Check Slack DM channel
        try
        {
            if (preferences is { SlackEnabled: true } && IsSlackEnabledForType(preferences, message.Type))
            {
                var slackConnection = await _slackConnectionRepository.GetActiveByUserIdAsync(message.RecipientUserId, ct).ConfigureAwait(false);
                if (slackConnection is { IsActive: true })
                {
                    var slackItem = NotificationQueueItem.Create(
                        channelType: NotificationChannelType.SlackUser,
                        recipientUserId: message.RecipientUserId,
                        notificationType: message.Type,
                        payload: message.Payload,
                        slackChannelTarget: slackConnection.DmChannelId,
                        slackTeamId: slackConnection.SlackTeamId,
                        correlationId: correlationId,
                        deepLinkPath: message.DeepLinkPath);

                    queueItems.Add(slackItem);

                    _logger.LogDebug(
                        "Enqueuing Slack DM notification for user {UserId}, team={TeamId}, channel={ChannelId}",
                        message.RecipientUserId, slackConnection.SlackTeamId, slackConnection.DmChannelId);
                }
            }
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve Slack DM channel for user {UserId}", message.RecipientUserId);
        }
#pragma warning restore CA1031

        // 5. Check Slack team channels from configuration
        try
        {
            var notificationTypeValue = message.Type.ToString();
            foreach (var (teamId, settings) in _slackConfig.TeamChannels)
            {
                if (settings.Types.Contains(notificationTypeValue, StringComparer.OrdinalIgnoreCase))
                {
                    var teamItem = NotificationQueueItem.Create(
                        channelType: NotificationChannelType.SlackTeam,
                        recipientUserId: null,
                        notificationType: message.Type,
                        payload: message.Payload,
                        slackChannelTarget: settings.WebhookUrl,
                        slackTeamId: teamId,
                        correlationId: correlationId,
                        deepLinkPath: message.DeepLinkPath);

                    queueItems.Add(teamItem);

                    _logger.LogDebug(
                        "Enqueuing Slack team notification for team={TeamId}, channel={Channel}, type={Type}",
                        teamId, settings.Channel, message.Type);
                }
            }
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve Slack team channels for notification type {Type}", message.Type);
        }
#pragma warning restore CA1031

        // 6. Persist all queue items
        if (queueItems.Count > 0)
        {
            await _notificationQueueRepository.AddRangeAsync(queueItems, ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Enqueued {Count} channel notification(s) for user {UserId}, correlationId={CorrelationId}",
                queueItems.Count, message.RecipientUserId, correlationId);
        }
    }

    /// <summary>
    /// Maps notification type to display severity.
    /// </summary>
    private static NotificationSeverity ResolveSeverity(NotificationType type)
    {
        if (type == NotificationType.DocumentProcessingFailed
            || type == NotificationType.AdminSystemHealthAlert
            || type == NotificationType.AdminOpenRouterThresholdAlert)
            return NotificationSeverity.Error;

        if (type == NotificationType.SessionTerminated
            || type == NotificationType.AdminReviewLockExpiring
            || type == NotificationType.AdminStaleShareRequests
            || type == NotificationType.RateLimitReached
            || type == NotificationType.SlackConnectionRevoked
            || type == NotificationType.MechanicAnalysisRejected)
            return NotificationSeverity.Warning;

        if (type == NotificationType.DocumentReady
            || type == NotificationType.RuleSpecGenerated
            || type == NotificationType.BadgeEarned
            || type == NotificationType.AgentReady
            || type == NotificationType.GdprDataExportReady
            || type == NotificationType.MechanicAnalysisReady)
            return NotificationSeverity.Success;

        return NotificationSeverity.Info;
    }

    /// <summary>
    /// Returns a human-readable title for the notification type.
    /// </summary>
    private static string ResolveTitle(NotificationType type)
    {
        if (type == NotificationType.DocumentReady) return "Documento pronto";
        if (type == NotificationType.RuleSpecGenerated) return "Specifiche generate";
        if (type == NotificationType.DocumentProcessingFailed) return "Elaborazione fallita";
        if (type == NotificationType.ShareRequestCreated) return "Nuova Share Request";
        if (type == NotificationType.ShareRequestApproved) return "Share Request approvata";
        if (type == NotificationType.ShareRequestRejected) return "Share Request rifiutata";
        if (type == NotificationType.ShareRequestChangesRequested) return "Modifiche richieste";
        if (type == NotificationType.BadgeEarned) return "Badge ottenuto";
        if (type == NotificationType.GameNightInvitation) return "Invito Serata";
        if (type == NotificationType.GameNightRsvpReceived) return "RSVP ricevuto";
        if (type == NotificationType.GameNightReminder) return "Promemoria Serata";
        if (type == NotificationType.GameNightCancelled) return "Serata annullata";
        if (type == NotificationType.AgentReady) return "Agente pronto";
        if (type == NotificationType.LoanReminder) return "Promemoria prestito";
        if (type == NotificationType.RateLimitApproaching) return "Quota in avvicinamento";
        if (type == NotificationType.RateLimitReached) return "Quota raggiunta";
        if (type == NotificationType.SessionTerminated) return "Sessione terminata";
        if (type == NotificationType.GdprDataExportReady) return "Export dati pronto";
        if (type == NotificationType.GdprAccountDeleted) return "Account eliminato";
        if (type == NotificationType.GdprAiConsentUpdated) return "Consenso AI aggiornato";
        if (type == NotificationType.SlackConnectionRevoked) return "Slack disconnesso";
        if (type == NotificationType.MechanicAnalysisReady) return "Analisi meccaniche pronta";
        if (type == NotificationType.MechanicAnalysisRejected) return "Analisi meccaniche rifiutata";

        // Admin types
        if (type == NotificationType.AdminNewShareRequest) return "[Admin] Nuova Share Request";
        if (type == NotificationType.AdminStaleShareRequests) return "[Admin] Share Request in attesa";
        if (type == NotificationType.AdminReviewLockExpiring) return "[Admin] Lock revisione in scadenza";
        if (type == NotificationType.AdminSharedGameSubmitted) return "[Admin] Gioco condiviso inviato";
        if (type == NotificationType.AdminOpenRouterThresholdAlert) return "[Admin] Soglia OpenRouter";
        if (type == NotificationType.AdminOpenRouterDailySummary) return "[Admin] Digest giornaliero";
        if (type == NotificationType.AdminSystemHealthAlert) return "[Admin] Alerta sistema";
        if (type == NotificationType.AdminModelStatusChanged) return "[Admin] Stato modello cambiato";
        if (type == NotificationType.AdminAccessRequestCreated) return "[Admin] Nuova richiesta accesso";
        if (type == NotificationType.AdminManualNotification) return "[Admin] Notifica manuale";
        if (type == NotificationType.AdminPdfProcessingStarted) return "[Admin] Elaborazione PDF avviata";

        return "Notifica MeepleAI";
    }

    /// <summary>
    /// Checks if email delivery is enabled for a given notification type based on user preferences.
    /// </summary>
    private static bool IsEmailEnabledForType(NotificationPreferences prefs, NotificationType type)
    {
        if (type == NotificationType.DocumentReady || type == NotificationType.RuleSpecGenerated)
            return prefs.EmailOnDocumentReady;
        if (type == NotificationType.DocumentProcessingFailed)
            return prefs.EmailOnDocumentFailed;
        if (type == NotificationType.GameNightInvitation || type == NotificationType.GameNightRsvpReceived)
            return prefs.EmailOnGameNightInvitation;
        if (type == NotificationType.GameNightReminder)
            return prefs.EmailOnGameNightReminder;

        // Default: send email for types not explicitly configured
        return true;
    }

    /// <summary>
    /// Checks if Slack DM delivery is enabled for a given notification type based on user preferences.
    /// Admin types are never delivered to individual users via Slack DM.
    /// </summary>
    private static bool IsSlackEnabledForType(NotificationPreferences prefs, NotificationType type)
    {
        // Admin types are never sent to individual users via DM
        if (type == NotificationType.AdminNewShareRequest
            || type == NotificationType.AdminStaleShareRequests
            || type == NotificationType.AdminReviewLockExpiring
            || type == NotificationType.AdminSharedGameSubmitted
            || type == NotificationType.AdminOpenRouterThresholdAlert
            || type == NotificationType.AdminOpenRouterDailySummary
            || type == NotificationType.AdminSystemHealthAlert
            || type == NotificationType.AdminModelStatusChanged
            || type == NotificationType.AdminAccessRequestCreated
            || type == NotificationType.AdminManualNotification
            || type == NotificationType.AdminPdfProcessingStarted)
            return false;

        if (type == NotificationType.DocumentReady || type == NotificationType.RuleSpecGenerated)
            return prefs.SlackOnDocumentReady;
        if (type == NotificationType.DocumentProcessingFailed)
            return prefs.SlackOnDocumentFailed;
        if (type == NotificationType.ShareRequestCreated)
            return prefs.SlackOnShareRequestCreated;
        if (type == NotificationType.ShareRequestApproved)
            return prefs.SlackOnShareRequestApproved;
        if (type == NotificationType.BadgeEarned)
            return prefs.SlackOnBadgeEarned;
        if (type == NotificationType.GameNightInvitation || type == NotificationType.GameNightRsvpReceived)
            return prefs.SlackOnGameNightInvitation;
        if (type == NotificationType.GameNightReminder)
            return prefs.SlackOnGameNightReminder;

        // Default: send Slack for types not explicitly configured
        return true;
    }
}
