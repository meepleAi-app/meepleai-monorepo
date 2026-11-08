using System.Diagnostics;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.Observability;

namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;

/// <summary>
/// #4453 (spec 2026-10-11 §6.3 e §7.3): i contatori dell'assistente, con i tag composti dal suo dominio. Gli esiti
/// di un turno che non ha risposto sono i codici che il turno salva già sul messaggio (<c>no_answer</c>,
/// <c>budget_paused</c>…), più <see cref="LoopExhausted"/>.
/// </summary>
internal static class SessionAssistantMetrics
{
    public const string Answered = "answered";

    /// <summary>
    /// Spec §6.3: i giri con strumenti esauriti senza una risposta. Il messaggio salvato è quello di una risposta
    /// vuota (<c>no_answer</c>), quindi solo la metrica li distingue.
    /// </summary>
    public const string LoopExhausted = "loop_exhausted";

    public static void Turn(string outcome, Guid profileVersionId) =>
        MeepleAiMetrics.SessionAssistantTurns.Add(1, new TagList
        {
            { "outcome", outcome },
            { "profile_version_id", profileVersionId.ToString() },
        });

    public static void ClaimedAction(Guid profileVersionId) =>
        MeepleAiMetrics.SessionAssistantClaimedActions.Add(1, new TagList
        {
            { "profile_version_id", profileVersionId.ToString() },
        });

    /// <summary>Da chiamare dopo il salvataggio dell'esito: un esito che non è nel database non entra nel tasso.</summary>
    public static void ProposalResolved(AssistantProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        MeepleAiMetrics.SessionAssistantProposalsResolved.Add(1, new TagList
        {
            { "outcome", OutcomeOf(proposal.Status) },
            { "kind", proposal.Kind == AssistantProposalKind.Score ? "score" : "advance_turn" },
            { "profile_version_id", proposal.ProfileVersionId.ToString() },
        });
    }

    private static string OutcomeOf(AssistantProposalStatus status) => status switch
    {
        AssistantProposalStatus.Accepted => "accepted",
        AssistantProposalStatus.Edited => "edited",
        AssistantProposalStatus.Rejected => "rejected",
        AssistantProposalStatus.Stale => "stale",
        AssistantProposalStatus.Failed => "failed",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Only a resolved proposal is counted."),
    };
}
