using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;

namespace Api.BoundedContexts.UserNotifications.Infrastructure.Email;

/// <summary>
/// Resolves a human-readable title for a <see cref="NotificationType"/>, used as the email subject
/// by <see cref="GenericEmailBuilder"/> (issue #3026).
///
/// NOTE: this intentionally mirrors the private <c>ResolveTitle</c> map in
/// <c>NotificationDispatcher</c>. Issue #3026 scoped the fix to add the missing email-channel
/// processor with ZERO changes to the dispatcher, so the map is duplicated here rather than
/// extracted. A follow-up cleanup should unify both call sites onto this resolver.
/// </summary>
internal static class NotificationTitleResolver
{
    public static string ResolveTitle(NotificationType type)
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
        if (type == NotificationType.AgentCreationFailed) return "Creazione agent fallita";
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
        if (type == NotificationType.AdminMechanicCardSuppressed) return "[Admin] Scheda Meccanica Soppressa";

        return "Notifica MeepleAI";
    }
}
