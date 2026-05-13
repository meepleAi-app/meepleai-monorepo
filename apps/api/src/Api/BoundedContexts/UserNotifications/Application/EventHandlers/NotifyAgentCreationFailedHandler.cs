using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.BoundedContexts.UserNotifications.Application.Services;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.UserNotifications.Application.EventHandlers;

/// <summary>
/// Handles <see cref="AutoAgentCreationFailedEvent"/> — the failure twin of
/// <see cref="AgentAutoCreatedEvent"/> (handled by <see cref="NotifyAgentReadyHandler"/>).
///
/// Fires when <c>AutoCreateAgentOnPdfReadyHandler</c> indexed a PDF successfully but
/// could not auto-create the user-facing agent (tier-quota slot exceeded, feature
/// locked behind a higher plan, or generic exception). Without this subscriber the
/// user sees no agent appear after PDF processing completed and has no idea why —
/// EPIC #906 SG1 surfaced the silent-failure; this handler closes the loop.
///
/// Issue #940 (EPIC #906 follow-up).
/// Spec-panel maturation 2026-05-13.
/// </summary>
internal sealed class NotifyAgentCreationFailedHandler : INotificationHandler<AutoAgentCreationFailedEvent>
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly ILogger<NotifyAgentCreationFailedHandler> _logger;

    public NotifyAgentCreationFailedHandler(
        INotificationDispatcher dispatcher,
        ILogger<NotifyAgentCreationFailedHandler> logger)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(AutoAgentCreationFailedEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        try
        {
            var (title, body, deepLink) = MapErrorToCopy(notification);

            await _dispatcher.DispatchAsync(new NotificationMessage
            {
                Type = NotificationType.AgentCreationFailed,
                RecipientUserId = notification.UserId,
                Payload = new GenericPayload(title, body),
                DeepLinkPath = deepLink
            }, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "NotifyAgentCreationFailedHandler: Dispatched failure notification for UserId={UserId}, " +
                "PdfDocumentId={PdfDocumentId}, GameId={GameId}, ErrorCode={ErrorCode}",
                notification.UserId,
                notification.PdfDocumentId,
                notification.GameId,
                notification.ErrorCode);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            // Defensive swallow: subscriber failure must NOT propagate back through
            // the MediatR INotification chain to the publisher's transaction
            // (AutoCreateAgentOnPdfReadyHandler's catch block). The original
            // creation failure has already been logged; this is best-effort UX.
            _logger.LogError(ex,
                "NotifyAgentCreationFailedHandler: Failed to dispatch failure notification for " +
                "UserId={UserId}, PdfDocumentId={PdfDocumentId}, ErrorCode={ErrorCode}. Notification skipped.",
                notification.UserId,
                notification.PdfDocumentId,
                notification.ErrorCode);
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Maps the event's <c>ErrorCode</c> to user-facing copy + a deep-link to the
    /// most useful next action. Italian copy matches the existing
    /// <see cref="NotifyAgentReadyHandler"/> convention (project default is
    /// <c>LOCALES.IT</c>; EN deferred to a future SSR locale-negotiation epic).
    /// </summary>
    private static (string Title, string Body, string DeepLink) MapErrorToCopy(AutoAgentCreationFailedEvent ev)
    {
        return ev.ErrorCode switch
        {
            "AGENT_SLOT_QUOTA_EXCEEDED" => (
                "Limite agent raggiunto",
                "Hai raggiunto il limite di agent del tuo piano. Esegui l'upgrade o elimina un agent esistente per crearne uno nuovo.",
                "/settings/subscription"
            ),
            "TIER_FEATURE_LOCKED" => (
                "Funzionalità non disponibile",
                "La creazione di agent richiede un piano superiore.",
                "/settings/subscription"
            ),
            // Fallback covers both the documented "AGENT_CREATION_FAILED" code and
            // any unknown ErrorCode value (defensive: don't crash on a value the
            // publisher adds after this handler ships).
            _ => (
                "Creazione agent fallita",
                "Non siamo riusciti a creare automaticamente un agent per il PDF. Riprova manualmente dal toolkit del gioco o contatta il supporto.",
                $"/library/private/{ev.GameId}/toolkit"
            )
        };
    }
}
