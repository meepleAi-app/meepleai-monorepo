using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Confirmation;

/// <summary>
/// #4455 (spec §5.2): a partita chiusa nessuna card agisce. Una conferma o un rifiuto su una proposta ancora in
/// attesa la fa scadere: niente prenotazione, niente scrittura della partita, e niente misura nella metrica delle
/// proposte, perché nessuno l'ha giudicata. Di solito l'ha già fatta scadere l'handler della chiusura
/// (<c>ExpireAssistantProposalsOnSessionCompletedHandler</c>, fra gli adattatori); questa è la guardia per quando
/// l'evento non è ancora arrivato (su staging parte dall'outbox, dopo il commit) o non è riuscito.
/// </summary>
internal static class ClosedSessionProposals
{
    public static ConflictException Expired() =>
        new(AssistantProposal.NotPendingCode, "The session is over: the proposal has expired.");

    /// <returns>
    /// <c>null</c> se l'ha fatta scadere questa richiesta; altrimenti la proposta com'è dopo una corsa persa su
    /// <c>xmin</c>, contro la chiusura o contro un altro tocco.
    /// </returns>
    public static async Task<AssistantProposal?> ExpireAsync(
        AssistantProposal proposal,
        IAssistantProposalRepository proposals,
        IUnitOfWork unitOfWork,
        DateTime now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentNullException.ThrowIfNull(proposals);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        proposal.Expire(now);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (DbUpdateConcurrencyException)
        {
            return await proposals.GetReadOnlyAsync(proposal.Id, cancellationToken).ConfigureAwait(false)
                ?? throw new NotFoundException("AssistantProposal", proposal.Id.ToString());
        }
    }
}
