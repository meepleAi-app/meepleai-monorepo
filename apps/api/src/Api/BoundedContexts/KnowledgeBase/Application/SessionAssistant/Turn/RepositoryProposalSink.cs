using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;

namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Turn;

/// <summary>
/// Il sink vero degli strumenti di proposta (spec §5.2): crea una <see cref="AssistantProposal"/> e la
/// passa al repository. Non salva: lo fa il turno, insieme ai messaggi. Un'istanza per turno, legata alla
/// sessione, al thread e al messaggio dell'assistente dal server.
/// </summary>
internal sealed class RepositoryProposalSink(
    IAssistantProposalRepository repository,
    Guid liveSessionId,
    Guid chatThreadId,
    Guid messageId,
    Guid profileVersionId,
    TimeProvider timeProvider) : IAssistantProposalSink
{
    private readonly List<AssistantProposal> _created = [];

    /// <summary>Le proposte di questo turno, nell'ordine in cui il modello le ha chieste.</summary>
    public IReadOnlyList<AssistantProposal> Created => _created;

    public Task<ProposalReceipt> ProposeScoreAsync(ScoreProposalDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        return AddAsync(
            AssistantProposal.ForScore(
                liveSessionId, chatThreadId, messageId, profileVersionId, draft.PlayerId, draft.PlayerName,
                draft.Dimension, draft.Round, draft.Points, draft.CellValueBefore, Now()),
            cancellationToken);
    }

    public Task<ProposalReceipt> ProposeTurnAsync(TurnProposalDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        return AddAsync(
            AssistantProposal.ForAdvanceTurn(
                liveSessionId, chatThreadId, messageId, profileVersionId, draft.CurrentTurnIndex, Now()),
            cancellationToken);
    }

    private async Task<ProposalReceipt> AddAsync(AssistantProposal proposal, CancellationToken cancellationToken)
    {
        await repository.AddAsync(proposal, cancellationToken).ConfigureAwait(false);
        _created.Add(proposal);
        return Receipt(proposal);
    }

    private static ProposalReceipt Receipt(AssistantProposal proposal) => new(proposal.Id, AssistantProposalSummary.For(proposal));

    private DateTime Now() => timeProvider.GetUtcNow().UtcDateTime;
}
