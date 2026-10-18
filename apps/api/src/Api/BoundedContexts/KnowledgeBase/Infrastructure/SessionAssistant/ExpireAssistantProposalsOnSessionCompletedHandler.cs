using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Confirmation;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.SessionAssistant;

/// <summary>
/// #4455 (spec §5.2): alla chiusura della partita le proposte ancora in attesa scadono, con lo stesso UPDATE della
/// scadenza di inizio turno, e chi riapre la pagina vede le card già «scadute». È pulizia: su staging l'evento parte
/// dall'outbox dopo il commit, e in quella finestra, o se questo handler non riesce, regge
/// <see cref="ClosedSessionProposals"/>. Un evento consegnato due volte non fa danni: l'UPDATE tocca solo le
/// proposte ancora <c>Pending</c>. Sta fra gli adattatori, come <see cref="LiveSessionProposalApplier"/>: il lato
/// applicativo dell'assistente non nomina GameManagement (spec §3.2).
/// </summary>
internal sealed class ExpireAssistantProposalsOnSessionCompletedHandler(IAssistantProposalRepository proposals)
    : INotificationHandler<LiveSessionCompletedEvent>
{
    public Task Handle(LiveSessionCompletedEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        return proposals.ExpirePendingBySessionAsync(notification.SessionId, notification.CompletedAt, cancellationToken);
    }
}
