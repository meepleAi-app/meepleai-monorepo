#pragma warning disable MA0048 // File name must match type name - the proposal port and its drafts
namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;

/// <summary>L'unica uscita degli strumenti di proposta: salva una proposta, non scrive la partita (spec §5.2).</summary>
internal interface IAssistantProposalSink
{
    Task<ProposalReceipt> ProposeScoreAsync(ScoreProposalDraft draft, CancellationToken cancellationToken);

    Task<ProposalReceipt> ProposeTurnAsync(TurnProposalDraft draft, CancellationToken cancellationToken);
}

/// <summary>Un incremento di punti sulla cella (giocatore, round, dimensione), con il valore della cella al momento.</summary>
internal sealed record ScoreProposalDraft(Guid PlayerId, string PlayerName, string Dimension, int Round, int Points, int CellValueBefore);

internal sealed record TurnProposalDraft(int CurrentTurnIndex, Guid? CurrentTurnPlayerId);

internal sealed record ProposalReceipt(Guid ProposalId, string Summary);
