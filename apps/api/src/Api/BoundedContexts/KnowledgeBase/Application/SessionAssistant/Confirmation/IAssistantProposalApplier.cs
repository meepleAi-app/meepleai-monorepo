#pragma warning disable MA0048 // File name must match type name - the write port, what it writes and its refusal
namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Confirmation;

/// <summary>
/// Un punteggio da scrivere: la cella (giocatore, round, dimensione), il valore che avrà, e il valore su
/// cui la proposta è nata. Una cella che non esiste vale zero.
/// </summary>
internal sealed record AssistantScoreWrite(Guid PlayerId, int Round, string Dimension, int Value, int ExpectedCurrentValue);

/// <summary>
/// L'unica porta dell'assistente che scrive la partita (spec 2026-10-11 §5.3). La riceve solo
/// <see cref="ConfirmAssistantProposalCommandHandler"/>, cioè un'azione avviata da una persona: lo fissa
/// un test architetturale. L'implementazione passa dai comandi esistenti della sessione live.
/// <para>
/// Ogni scrittura porta lo stato su cui la proposta è nata, e la sessione lo ricontrolla sull'aggregato
/// che sta per salvare: il controllo fatto da chi conferma e la scrittura sono due letture distinte, e fra
/// le due può passare un'altra conferma.
/// </para>
/// </summary>
internal interface IAssistantProposalApplier
{
    /// <summary>Scrive il valore nella cella e lo trasmette ai partecipanti, se la cella ha ancora il valore atteso.</summary>
    /// <exception cref="AssistantProposalApplyException">La sessione ha rifiutato la scrittura.</exception>
    Task ApplyScoreAsync(
        Guid liveSessionId, Guid confirmingUserId, AssistantScoreWrite write, CancellationToken cancellationToken);

    /// <summary>Passa il turno, se l'indice di turno è ancora quello atteso.</summary>
    /// <exception cref="AssistantProposalApplyException">La sessione ha rifiutato il passaggio di turno.</exception>
    Task AdvanceTurnAsync(Guid liveSessionId, int expectedTurnIndex, CancellationToken cancellationToken);
}

/// <summary>
/// La sessione ha rifiutato ciò che la proposta voleva scrivere: nulla è stato scritto. Il codice dice
/// perché, e decide l'esito della proposta.
/// </summary>
#pragma warning disable S3871 // Exceptions should be public — internal here is intentional (BC-private contract)
internal sealed class AssistantProposalApplyException(string failureCode, string message, Exception innerException)
    : Exception(message, innerException)
{
    /// <summary>Lo stato della sessione non permette la scrittura (per esempio è conclusa), o è cambiata nello stesso istante.</summary>
    public const string SessionStateInvalid = "session_state_invalid";

    /// <summary>La proposta non è più applicabile a questa partita: il giocatore è uscito, la dimensione non c'è più.</summary>
    public const string NotApplicable = "proposal_not_applicable";

    /// <summary>
    /// La cella o l'indice di turno non sono più quelli su cui la proposta era nata: la proposta è
    /// superata (<c>Stale</c>), non fallita. È lo stesso codice del controllo fatto prima di scrivere.
    /// </summary>
    public const string GameChanged = "proposal_stale";

    public string FailureCode { get; } = failureCode;
}
#pragma warning restore S3871
