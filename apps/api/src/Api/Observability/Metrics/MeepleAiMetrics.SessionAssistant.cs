// Issue #4453 (epic #4238, A6): le metriche dell'assistente in partita (spec 2026-10-11 §6.3 e §7.3).
using System.Diagnostics.Metrics;

namespace Api.Observability;

internal static partial class MeepleAiMetrics
{
    // Nel pilota si osservano, non sono un gate (spec §7.3). I tag sono insiemi limitati: profile_version_id è un
    // id, ma ne nasce uno per pubblicazione del profilo. Niente id di sessione, di utente o di gioco.
    // I tag li compone SessionAssistantMetrics, dal dominio dell'assistente.

    /// <summary>
    /// Un turno dell'assistente, contato una volta quando finisce: dopo il salvataggio della conversazione, o quando
    /// fallisce. Tag: <c>outcome</c> (answered, loop_exhausted, no_answer, budget_paused, turn_timeout,
    /// assistant_unavailable, copyright_sanitized), <c>profile_version_id</c>.
    /// Prometheus: <c>meepleai_session_assistant_turns_total</c>.
    /// </summary>
    public static readonly Counter<long> SessionAssistantTurns = Meter.CreateCounter<long>(
        name: "meepleai.session_assistant.turns.total",
        unit: "turns",
        description: "Session assistant turns by outcome and profile version");

    /// <summary>
    /// Spec §6.3, <c>claimed_action_without_proposal</c>: una risposta che dichiara un'azione, o una proposta, che il
    /// turno non ha creato. Tag: <c>profile_version_id</c>.
    /// Prometheus: <c>meepleai_session_assistant_claimed_actions_answers_total</c>.
    /// </summary>
    public static readonly Counter<long> SessionAssistantClaimedActions = Meter.CreateCounter<long>(
        name: "meepleai.session_assistant.claimed_actions.total",
        unit: "answers",
        description: "Session assistant answers that claim an action or a proposal the turn did not create");

    /// <summary>
    /// Una proposta chiusa dall'host o dal dominio, contata dopo il salvataggio dell'esito. Tag: <c>outcome</c>
    /// (accepted, edited, rejected, stale, failed), <c>kind</c> (score, advance_turn), <c>profile_version_id</c>.
    /// Il tasso di accettazione di §7.3 è accepted / (accepted + edited + rejected), per versione del profilo.
    /// Prometheus: <c>meepleai_session_assistant_proposals_resolved_proposals_total</c>.
    /// </summary>
    public static readonly Counter<long> SessionAssistantProposalsResolved = Meter.CreateCounter<long>(
        name: "meepleai.session_assistant.proposals.resolved.total",
        unit: "proposals",
        description: "Session assistant proposals resolved by the host or the domain, by outcome, kind and profile version");
}
