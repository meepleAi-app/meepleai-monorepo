#pragma warning disable MA0048 // File name must match type name - the write port and its refusal
namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Confirmation;

/// <summary>
/// L'unica porta dell'assistente che scrive la partita (spec 2026-10-11 §5.3). La riceve solo
/// <see cref="ConfirmAssistantProposalCommandHandler"/>, cioè un'azione avviata da una persona: lo fissa
/// un test architetturale. L'implementazione passa dai comandi esistenti della sessione live.
/// </summary>
internal interface IAssistantProposalApplier
{
    /// <summary>Scrive <paramref name="value"/> nella cella (giocatore, round, dimensione) e lo trasmette ai partecipanti.</summary>
    /// <exception cref="AssistantProposalApplyException">La sessione ha rifiutato la scrittura.</exception>
    Task ApplyScoreAsync(
        Guid liveSessionId, Guid confirmingUserId, Guid playerId, int round, string dimension, int value,
        CancellationToken cancellationToken);

    /// <exception cref="AssistantProposalApplyException">La sessione ha rifiutato il passaggio di turno.</exception>
    Task AdvanceTurnAsync(Guid liveSessionId, CancellationToken cancellationToken);
}

/// <summary>
/// La sessione ha rifiutato ciò che la proposta voleva scrivere. Il codice finisce in
/// <c>failure_code</c> della proposta e nel 409 della risposta.
/// </summary>
#pragma warning disable S3871 // Exceptions should be public — internal here is intentional (BC-private contract)
internal sealed class AssistantProposalApplyException(string failureCode, string message, Exception innerException)
    : Exception(message, innerException)
{
    /// <summary>Lo stato della sessione non permette la scrittura (per esempio è conclusa), o è cambiata nel frattempo.</summary>
    public const string SessionStateInvalid = "session_state_invalid";

    /// <summary>La proposta non è più applicabile a questa partita: il giocatore è uscito, la dimensione non c'è più.</summary>
    public const string NotApplicable = "proposal_not_applicable";

    public string FailureCode { get; } = failureCode;
}
#pragma warning restore S3871
