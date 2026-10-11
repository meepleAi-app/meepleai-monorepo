#pragma warning disable MA0048 // File name must match type name - the gate contract: interface, request, outcome
using Api.BoundedContexts.KnowledgeBase.Domain.Services.LlmManagement;
using Api.Services;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// ADR-095 D4 e D6 (#4166): il controllo che il gateway LLM fa <b>prima</b> di ogni chiamata a un
/// provider. Rifiuta un modello che il catalogo non prezza (fail closed) e una chiamata il cui costo
/// massimo stimato porterebbe oltre il tetto giornaliero o mensile, o oltre la quota della sua classe.
/// </summary>
/// <remarks>
/// Ogni ammissione riserva il costo stimato finché il gateway non chiama <see cref="Complete"/> con
/// il costo reale: due chiamate concorrenti non possono passare entrambe sullo stesso margine.
/// </remarks>
internal interface ILlmBudgetGate
{
    /// <summary>Decide se la chiamata può partire; se sì, ne riserva il costo stimato.</summary>
    Task<LlmBudgetAdmission> AdmitAsync(LlmBudgetRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Chiude un'ammissione con il costo reale della chiamata (zero se è fallita). Va chiamato una
    /// volta per ogni ammissione, anche rifiutata: in quel caso non fa nulla.
    /// </summary>
    /// <param name="admission">L'ammissione da chiudere.</param>
    /// <param name="actualCostUsd">Il costo della chiamata.</param>
    /// <param name="recordedInCostLog">
    /// Falso quando il costo non arriverà nel registro: uno stream interrotto dal consumatore prima
    /// del chunk finale. Il provider lo fattura comunque; il gate lo conta con la stima fino alla
    /// fine del mese, invece di dimenticarlo alla lettura successiva del registro.
    /// </param>
    void Complete(LlmBudgetAdmission admission, decimal actualCostUsd, bool recordedInCostLog = true);

    /// <summary>
    /// #4166 (ADR-095 D6): vero se il consumo del giorno ha raggiunto la soglia di degrado. Fra la
    /// soglia e il tetto le risposte passano al modello di riserva del profilo e il background si
    /// ferma; oltre il tetto non parte nulla.
    /// </summary>
    Task<bool> IsDegradedAsync(CancellationToken cancellationToken);

    /// <summary>
    /// #4166 (review della 3b): vero se il degrado può passare dal modello instradato alla riserva,
    /// cioè se la riserva è nel catalogo e costa meno. Una riserva fuori catalogo verrebbe rifiutata
    /// (D5), e una più cara, o al posto di un modello gratuito, alzerebbe la spesa che il degrado
    /// deve ridurre. Un modello instradato fuori catalogo verrebbe rifiutato: la riserva lo sostituisce.
    /// </summary>
    Task<bool> IsCheaperReserveAsync(
        string routedModel, string routedProvider, string reserveModel, string reserveProvider, CancellationToken cancellationToken);
}

/// <summary>
/// #4166: una chiamata che il gateway sta per fare.
/// </summary>
/// <param name="Source">La sorgente, che decide la classe di budget.</param>
/// <param name="ModelId">Il modello che verrà chiesto al provider.</param>
/// <param name="Provider">Il provider, per i modelli in casa che non hanno prezzo per token.</param>
/// <param name="PromptCharacters">I caratteri del prompt (sistema e utente), da cui si stimano i token in ingresso.</param>
/// <param name="MaxOutputTokens">La lunghezza massima della risposta: il costo in uscita si stima su questa.</param>
/// <param name="ImageCount">Le immagini del messaggio, per le chiamate multimodali.</param>
internal sealed record LlmBudgetRequest(
    RequestSource Source,
    string ModelId,
    string Provider,
    int PromptCharacters,
    int MaxOutputTokens,
    int ImageCount = 0);

/// <summary>
/// #4166: perché il gateway non fa partire una chiamata.
/// </summary>
internal enum LlmBudgetRejection
{
    /// <summary>La chiamata può partire.</summary>
    None,

    /// <summary>Il catalogo non prezza il modello: la chiamata sarebbe registrata a $0 (D5, fail closed).</summary>
    ModelNotInCatalog,

    /// <summary>Raggiunto il tetto giornaliero.</summary>
    DailyCapReached,

    /// <summary>Raggiunto il tetto mensile.</summary>
    MonthlyCapReached,

    /// <summary>La classe della chiamata ha esaurito la sua quota del giorno.</summary>
    DailyQuotaReached,

    /// <summary>La classe della chiamata ha esaurito la sua quota del mese.</summary>
    MonthlyQuotaReached,

    /// <summary>
    /// Il consumo del giorno ha raggiunto la soglia di degrado: background e valutazioni sono in
    /// pausa fino a domani, per lasciare il resto del budget alle risposte.
    /// </summary>
    BackgroundPaused,

    /// <summary>Il catalogo o la spesa non si sono potuti leggere: senza, il tetto non è verificabile.</summary>
    BudgetUnavailable,
}

/// <summary>
/// #4166: l'esito del controllo del gateway. Un'ammissione concessa porta la riserva da chiudere con
/// <see cref="ILlmBudgetGate.Complete"/>.
/// </summary>
internal sealed record LlmBudgetAdmission
{
    /// <summary>Codice d'errore per una chiamata rifiutata dal tetto o dalla quota del giorno: riprende domani.</summary>
    public const string DailyBudgetErrorCode = "AI_BUDGET_DAILY_EXHAUSTED";

    /// <summary>Codice d'errore per una chiamata rifiutata dal tetto o dalla quota del mese.</summary>
    public const string MonthlyBudgetErrorCode = "AI_BUDGET_MONTHLY_EXHAUSTED";

    /// <summary>Codice d'errore esposto ai client per un modello fuori catalogo.</summary>
    public const string ModelErrorCode = "AI_MODEL_NOT_IN_CATALOG";

    /// <summary>Codice d'errore esposto ai client quando il tetto non si può verificare.</summary>
    public const string UnavailableErrorCode = "AI_BUDGET_UNAVAILABLE";

    public required Guid ReservationId { get; init; }
    public required LlmBudgetRejection Rejection { get; init; }
    public required LlmBudgetClass BudgetClass { get; init; }
    public required decimal EstimatedCostUsd { get; init; }

    /// <summary>Il motivo del rifiuto, leggibile; <c>null</c> se la chiamata è ammessa.</summary>
    public string? Message { get; init; }

    public bool IsAllowed => Rejection == LlmBudgetRejection.None;

    /// <summary>Il codice d'errore per i client, o <c>null</c> se la chiamata è ammessa.</summary>
    public string? ErrorCode => Rejection switch
    {
        LlmBudgetRejection.None => null,
        LlmBudgetRejection.ModelNotInCatalog => ModelErrorCode,
        LlmBudgetRejection.BudgetUnavailable => UnavailableErrorCode,
        LlmBudgetRejection.MonthlyCapReached or LlmBudgetRejection.MonthlyQuotaReached => MonthlyBudgetErrorCode,
        _ => DailyBudgetErrorCode,
    };

    /// <summary>Un'ammissione senza controllo, per un gateway costruito senza gate (solo nei test).</summary>
    public static LlmBudgetAdmission Unchecked(LlmBudgetClass budgetClass) => new()
    {
        ReservationId = Guid.Empty,
        Rejection = LlmBudgetRejection.None,
        BudgetClass = budgetClass,
        EstimatedCostUsd = 0m,
    };
}
