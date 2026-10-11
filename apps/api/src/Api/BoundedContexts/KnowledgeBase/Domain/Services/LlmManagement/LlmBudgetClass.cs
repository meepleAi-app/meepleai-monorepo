using Api.Services;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.LlmManagement;

/// <summary>
/// ADR-095 D6 (#4166): a quale quota del tetto unico va una chiamata LLM.
/// </summary>
/// <remarks>
/// Le risposte agli utenti possono usare tutto il tetto. Il background e le valutazioni hanno
/// ciascuno una quota (<c>LlmSystemConfig.BackgroundQuotaPercent</c> ed
/// <c>EvaluationQuotaPercent</c>), così non consumano il budget delle risposte.
/// </remarks>
internal enum LlmBudgetClass
{
    /// <summary>Una risposta a una richiesta dell'utente.</summary>
    Answer,

    /// <summary>Lavoro di sistema o dell'admin, che nessun utente sta aspettando.</summary>
    Background,

    /// <summary>Valutazione della qualità di modelli e risposte.</summary>
    Evaluation,
}

/// <summary>
/// #4166: la classe di budget di ogni <see cref="RequestSource"/>.
/// </summary>
internal static class LlmBudgetClassification
{
    /// <summary>
    /// La classe di budget della sorgente. Ogni valore è elencato: un valore nuovo di
    /// <see cref="RequestSource"/> deve essere classificato, non ricevere una classe per caso.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">La sorgente non è classificata.</exception>
    public static LlmBudgetClass For(RequestSource source) => source switch
    {
        // Ciò che l'utente sta aspettando: la risposta, la sua classificazione per l'instradamento,
        // la traduzione del librogame e l'assistente in sessione. AgentTask, nonostante il nome, è la
        // sorgente della chat dell'agente di sessione, della risposta ancorata, dell'arbitro e del
        // riepilogo del turno: tutte cose che l'utente aspetta.
        RequestSource.Manual => LlmBudgetClass.Answer,
        RequestSource.RagPipeline => LlmBudgetClass.Answer,
        RequestSource.RagClassification => LlmBudgetClass.Answer,
        RequestSource.Translation => LlmBudgetClass.Answer,
        RequestSource.AgentTask => LlmBudgetClass.Answer,

        // Lavoro di sistema o dell'admin: traduzione dei chunk in indicizzazione, estrattore di
        // meccaniche e playground (AdminOperation), eventi, test automatici.
        RequestSource.ChunkTranslation => LlmBudgetClass.Background,
        RequestSource.AdminOperation => LlmBudgetClass.Background,
        RequestSource.EventDriven => LlmBudgetClass.Background,
        RequestSource.AutomatedTest => LlmBudgetClass.Background,

        // Valutazioni: KbQuality e il confronto A/B fra modelli.
        RequestSource.Evaluation => LlmBudgetClass.Evaluation,
        RequestSource.ABTesting => LlmBudgetClass.Evaluation,

        _ => throw new ArgumentOutOfRangeException(nameof(source), source,
            "Every RequestSource needs a budget class (ADR-095 D6): add it to LlmBudgetClassification")
    };
}
