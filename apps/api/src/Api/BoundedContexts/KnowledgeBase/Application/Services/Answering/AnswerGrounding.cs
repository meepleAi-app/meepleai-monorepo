using Api.SharedKernel.Domain.Enums;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services.Answering;

/// <summary>
/// #3388, #4481: lo stato di grounding di una risposta, uno per tutti i punti che lo calcolano.
/// <see cref="GroundingStatus.Grounded"/> se la risposta porta almeno una fonte e non è la rinuncia
/// (<see cref="RulebookRefusal.IsRefusal"/>); altrimenti <see cref="GroundingStatus.Ungrounded"/>.
/// </summary>
/// <remarks>
/// Prima contava solo il numero di fonti. Il recupero trova quasi sempre qualcosa, quindi anche la
/// rinuncia risultava «Grounded»: nella misura del 2026-10-18, 12 risposte su 29 su Catan. Una rinuncia
/// non afferma niente dalle fonti, quindi non è ancorata; le fonti trovate restano nell'evento delle
/// citazioni. Una risposta parziale con la rinuncia in coda resta «Grounded»: quello che dice viene
/// dalle fonti.
/// </remarks>
internal static class AnswerGrounding
{
    internal static GroundingStatus Of(string? answer, int citationCount) =>
        citationCount > 0 && !RulebookRefusal.IsRefusal(answer)
            ? GroundingStatus.Grounded
            : GroundingStatus.Ungrounded;

    /// <summary>Il valore sullo stream: il nome dell'enum, come vuole il contratto di <c>StreamingComplete</c>.</summary>
    internal static string WireOf(string? answer, int citationCount) => Of(answer, citationCount).ToString();
}
