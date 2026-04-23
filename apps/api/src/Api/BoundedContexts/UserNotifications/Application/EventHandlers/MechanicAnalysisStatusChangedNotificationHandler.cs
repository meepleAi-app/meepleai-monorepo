using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Application.Services;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.UserNotifications.Application.EventHandlers;

/// <summary>
/// Handles <see cref="MechanicAnalysisStatusChangedEvent"/> for the M1.2 pipeline
/// (ISSUE-524 / ADR-051) and dispatches user-facing notifications on terminal transitions:
/// <list type="bullet">
///   <item><c>Draft → InReview</c>: pipeline succeeded, fires <see cref="NotificationType.MechanicAnalysisReady"/>.</item>
///   <item><c>* → Rejected</c>: pipeline auto-rejected (e.g. cost cap), fires <see cref="NotificationType.MechanicAnalysisRejected"/>.</item>
/// </list>
/// Other transitions (Rejected→InReview moderator reopen, Draft→Rejected→...) are intentionally silent.
/// Delivery is multi-channel via <see cref="INotificationDispatcher"/> (in-app + email + Slack
/// when enabled), and the real-time push to admin dashboards is handled downstream by the
/// <c>IUserNotificationBroadcaster</c> publish performed inside <c>INotificationRepository.AddAsync</c>.
/// </summary>
internal sealed class MechanicAnalysisStatusChangedNotificationHandler
    : DomainEventHandlerBase<MechanicAnalysisStatusChangedEvent>
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly IMechanicAnalysisRepository _analysisRepository;

    public MechanicAnalysisStatusChangedNotificationHandler(
        MeepleAiDbContext dbContext,
        INotificationDispatcher dispatcher,
        IMechanicAnalysisRepository analysisRepository,
        ILogger<MechanicAnalysisStatusChangedNotificationHandler> logger)
        : base(dbContext, logger)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _analysisRepository = analysisRepository ?? throw new ArgumentNullException(nameof(analysisRepository));
    }

    protected override async Task HandleEventAsync(
        MechanicAnalysisStatusChangedEvent domainEvent,
        CancellationToken cancellationToken)
    {
        // Only terminal user-facing transitions produce a notification.
        if (domainEvent.ToStatus is not MechanicAnalysisStatus.InReview
            and not MechanicAnalysisStatus.Rejected)
        {
            return;
        }

        // Draft → InReview is the pipeline success path. Rejected → InReview is a reopen
        // performed by a moderator, which is not a user-facing lifecycle event.
        if (domainEvent.ToStatus == MechanicAnalysisStatus.InReview
            && domainEvent.FromStatus != MechanicAnalysisStatus.Draft)
        {
            return;
        }

        var analysis = await _analysisRepository
            .GetByIdAsync(domainEvent.AnalysisId, cancellationToken)
            .ConfigureAwait(false);

        if (analysis is null)
        {
            Logger.LogWarning(
                "MechanicAnalysis {AnalysisId} not found when dispatching status-changed notification ({FromStatus} → {ToStatus})",
                domainEvent.AnalysisId,
                domainEvent.FromStatus,
                domainEvent.ToStatus);
            return;
        }

        var isReady = domainEvent.ToStatus == MechanicAnalysisStatus.InReview;
        var notificationType = isReady
            ? NotificationType.MechanicAnalysisReady
            : NotificationType.MechanicAnalysisRejected;

        var title = isReady
            ? "Analisi meccaniche pronta"
            : "Analisi meccaniche rifiutata";

        var body = isReady
            ? "Il pipeline di estrazione meccaniche è stato completato e l'analisi è pronta per la revisione admin."
            : BuildRejectionBody(analysis.RejectionReason, domainEvent.Note);

        await _dispatcher.DispatchAsync(new NotificationMessage
        {
            Type = notificationType,
            RecipientUserId = analysis.CreatedBy,
            Payload = new GenericPayload(title, body),
            DeepLinkPath = $"/admin/mechanic-analyses/{domainEvent.AnalysisId}/review"
        }, cancellationToken).ConfigureAwait(false);

        Logger.LogInformation(
            "Dispatched {NotificationType} for MechanicAnalysis {AnalysisId} (recipient={UserId}, {FromStatus} → {ToStatus})",
            notificationType,
            domainEvent.AnalysisId,
            analysis.CreatedBy,
            domainEvent.FromStatus,
            domainEvent.ToStatus);
    }

    protected override Guid? GetUserId(MechanicAnalysisStatusChangedEvent domainEvent)
        => domainEvent.ActorId;

    protected override Dictionary<string, object?>? GetAuditMetadata(
        MechanicAnalysisStatusChangedEvent domainEvent)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["AnalysisId"] = domainEvent.AnalysisId,
            ["FromStatus"] = domainEvent.FromStatus.ToString(),
            ["ToStatus"] = domainEvent.ToStatus.ToString(),
            ["ActorId"] = domainEvent.ActorId,
            ["Action"] = "MechanicAnalysisStatusChangedNotification"
        };
    }

    private static string BuildRejectionBody(string? rejectionReason, string? note)
    {
        var reason = !string.IsNullOrWhiteSpace(rejectionReason)
            ? rejectionReason
            : note;

        return string.IsNullOrWhiteSpace(reason)
            ? "Il pipeline di estrazione meccaniche è stato interrotto e l'analisi è stata rifiutata automaticamente."
            : $"L'analisi meccaniche è stata rifiutata automaticamente. Motivo: {reason}";
    }
}
