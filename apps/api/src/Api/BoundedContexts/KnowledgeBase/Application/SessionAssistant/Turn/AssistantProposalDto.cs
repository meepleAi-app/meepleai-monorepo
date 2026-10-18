using System.Globalization;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;

#pragma warning disable MA0048 // File name must match type name - the proposal as the client sees it, and its summary
namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Turn;

/// <summary>
/// Una proposta come la vede il client: nell'evento <c>proposal</c> del turno e nella risposta di conferma
/// e rifiuto. <c>Kind</c> e <c>Status</c> sono stringhe, perché le opzioni JSON degli eventi SSE scrivono
/// gli enum come numeri. Per una proposta <c>Score</c>, <c>ResultingValue</c> è il valore che la cella
/// avrà (o ha) dopo la conferma.
/// </summary>
internal sealed record AssistantProposalDto(
    Guid Id,
    string Kind,
    string Status,
    Guid MessageId,
    Guid? PlayerId,
    string? PlayerName,
    string? Dimension,
    int? Round,
    int? Points,
    int? CellValueBefore,
    int? ResultingValue,
    int? ExpectedTurnIndex,
    int? ConfirmedPoints,
    string? FailureCode,
    string Summary)
{
    public static AssistantProposalDto From(AssistantProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        var confirmed = proposal.ConfirmedPayload?.Points;
        return new AssistantProposalDto(
            proposal.Id,
            proposal.Kind.ToString(),
            proposal.Status.ToString(),
            proposal.MessageId,
            proposal.Payload.PlayerId,
            proposal.Payload.PlayerName,
            proposal.Payload.Dimension,
            proposal.Payload.Round,
            proposal.Payload.Points,
            proposal.Expected.CellValue,
            proposal.Kind == AssistantProposalKind.Score ? proposal.ScoreValueToWrite(confirmed) : null,
            proposal.Expected.TurnIndex,
            confirmed,
            proposal.FailureCode,
            AssistantProposalSummary.For(proposal));
    }
}

/// <summary>
/// Il testo di una proposta e del suo esito, per il modello: la ricevuta dello strumento e la storia dei
/// turni passati (spec §6.2). In italiano, come le descrizioni degli strumenti.
/// </summary>
internal static class AssistantProposalSummary
{
    public static string For(AssistantProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        if (proposal.Kind == AssistantProposalKind.AdvanceTurn)
        {
            return "Passare il turno al giocatore successivo";
        }

        var points = proposal.ConfirmedPayload?.Points ?? proposal.Payload.Points ?? 0;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{points:+0;-0;+0} {proposal.Payload.Dimension} a {proposal.Payload.PlayerName}, round {proposal.Payload.Round}");
    }

    public static string Outcome(AssistantProposalStatus status) => status switch
    {
        AssistantProposalStatus.Pending => "in attesa di conferma",
        AssistantProposalStatus.Applying => "in applicazione",
        AssistantProposalStatus.Accepted => "confermata dall'host",
        AssistantProposalStatus.Edited => "confermata dall'host con una correzione",
        AssistantProposalStatus.Failed => "non applicata",
        AssistantProposalStatus.Rejected => "rifiutata dall'host",
        AssistantProposalStatus.Expired => "scaduta senza conferma",
        AssistantProposalStatus.Stale => "superata: la partita era cambiata",
        _ => status.ToString(),
    };
}
