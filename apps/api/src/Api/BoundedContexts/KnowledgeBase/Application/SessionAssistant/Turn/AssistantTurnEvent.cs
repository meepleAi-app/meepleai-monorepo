#pragma warning disable MA0048 // File name must match type name - the turn event and the fallback texts
namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Turn;

/// <summary>
/// Un evento del turno (spec §6.2), scritto come <c>data:</c> senza nome con un campo <c>type</c>, nella
/// convenzione degli endpoint SSE esistenti. Un solo record con campi opzionali: ogni tipo ne usa alcuni.
/// </summary>
/// <remarks>
/// <c>Type</c> è <c>status</c>, <c>proposal</c>, <c>answer</c>, <c>done</c> o <c>error</c>. <c>Tool</c> vale
/// per <c>status</c>: lo strumento che sta girando, assente per l'avvio del turno. <c>Proposal</c> per
/// <c>proposal</c>, <c>Text</c> per <c>answer</c>. <c>ResponseId</c> (l'id del messaggio dell'assistente) e la
/// versione del profilo per <c>done</c>. <c>Code</c> per <c>error</c>: <c>budget_paused</c>,
/// <c>turn_timeout</c> o <c>assistant_unavailable</c>. <c>Citations</c> per <c>answer</c>, dalla fetta A5 (#4411): le
/// fonti della risposta, già filtrate per tier.
/// </remarks>
internal sealed record AssistantTurnEvent(
    string Type,
    string? Tool = null,
    AssistantProposalDto? Proposal = null,
    string? Text = null,
    Guid? ResponseId = null,
    Guid? ProfileVersionId = null,
    int? ProfileVersionNumber = null,
    string? Code = null,
    IReadOnlyList<Api.Models.CitationDto>? Citations = null)
{
    public const string StatusType = "status";
    public const string ProposalType = "proposal";
    public const string AnswerType = "answer";
    public const string DoneType = "done";
    public const string ErrorType = "error";

    public static AssistantTurnEvent Status(string? tool = null) => new(StatusType, Tool: tool);

    public static AssistantTurnEvent ForProposal(AssistantProposalDto proposal) => new(ProposalType, Proposal: proposal);

    public static AssistantTurnEvent Answer(string text, IReadOnlyList<Api.Models.CitationDto>? citations = null) =>
        new(AnswerType, Text: text, Citations: citations is { Count: > 0 } ? citations : null);

    public static AssistantTurnEvent Done(Guid responseId, Guid profileVersionId, int profileVersionNumber) =>
        new(DoneType, ResponseId: responseId, ProfileVersionId: profileVersionId, ProfileVersionNumber: profileVersionNumber);

    public static AssistantTurnEvent Error(string code) => new(ErrorType, Code: code);
}

/// <summary>
/// I testi che il turno scrive al posto del modello (spec §6.3): quando il modello non ha risposto, e
/// l'avviso quando dice di aver fatto qualcosa che non ha fatto. Finiscono nella conversazione salvata,
/// quindi sono nella lingua della sessione.
/// </summary>
internal static class AssistantTurnTexts
{
    public static string ForError(string code, bool english, bool hasProposals)
    {
        var what = code switch
        {
            AssistantTurnRunner.BudgetPausedCode => english
                ? "The assistant is paused: the spending cap has been reached."
                : "L'assistente è in pausa: il tetto di spesa è stato raggiunto.",
            AssistantTurnRunner.TimeoutCode => english
                ? "I could not answer in time."
                : "Non ho fatto in tempo a rispondere.",
            _ => english
                ? "The assistant is not available right now."
                : "L'assistente non è disponibile in questo momento.",
        };
        var retryMakesSense = !string.Equals(code, AssistantTurnRunner.BudgetPausedCode, StringComparison.Ordinal);
        return $"{what} {WhatNow(english, hasProposals, retryMakesSense)}";
    }

    public static string NoAnswer(bool english, bool hasProposals) =>
        (english ? "I could not complete the answer. " : "Non sono riuscito a completare la risposta. ")
        + WhatNow(english, hasProposals, retryMakesSense: true);

    public static string NothingRecorded(bool english) => english
        ? "\n\nNote: nothing has been recorded and there is no proposal to confirm. Ask again, or record it by hand."
        : "\n\nAttenzione: non è stato registrato nulla e non c'è nessuna proposta da confermare. Ripeti la richiesta, o segna a mano.";

    /// <summary>
    /// Con una card davanti, mandare l'host a segnare a mano gli farebbe scrivere due volte: le proposte
    /// già create restano valide (spec §6.3).
    /// </summary>
    private static string WhatNow(bool english, bool hasProposals, bool retryMakesSense)
    {
        if (hasProposals)
        {
            return english
                ? "The proposals already prepared are still valid: confirm them from their cards."
                : "Le proposte già preparate restano valide: confermale dalle card.";
        }

        if (retryMakesSense)
        {
            return english ? "Try again, or record it by hand." : "Riprova, o segna a mano.";
        }

        return english
            ? "You can still record scores and turns by hand."
            : "Puoi comunque segnare a mano punteggi e turni.";
    }
}
