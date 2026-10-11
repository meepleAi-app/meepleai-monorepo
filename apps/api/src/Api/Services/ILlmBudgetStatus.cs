#pragma warning disable MA0048 // File name must match type name - interface with its result records
namespace Api.Services;

/// <summary>
/// ADR-095 D6 (#4166): quanto resta del budget LLM per una sorgente, letto dal gateway.
/// </summary>
/// <remarks>
/// Serve a chi deve decidere prima di cominciare un lavoro lungo, come una valutazione di
/// KbQuality che fa molte chiamate: il controllo per chiamata del gateway lo fermerebbe a metà.
/// Sta qui, accanto a <see cref="ILlmService"/>, perché lo usano anche contesti diversi da
/// KnowledgeBase.
/// </remarks>
public interface ILlmBudgetStatus
{
    /// <summary>
    /// Il budget che resta, in USD, alla classe della sorgente: il minimo fra tetto giornaliero,
    /// tetto mensile e, per background e valutazioni, la loro quota del giorno e del mese.
    /// Zero se la spesa non si può leggere.
    /// </summary>
    Task<decimal> GetRemainingUsdAsync(RequestSource source, CancellationToken cancellationToken);

    /// <summary>
    /// #4169 (ADR-095 D8): la spesa che il gateway conta oggi e nel mese, per classe, e se il degrado
    /// è attivo. È la stessa vista con cui il gate decide: il registro più le chiamate chiuse e quelle
    /// in corso dopo l'ultima lettura. <c>null</c> se la spesa non si può leggere.
    /// </summary>
    Task<LlmBudgetUsage?> GetUsageAsync(CancellationToken cancellationToken);
}

/// <summary>#4169: il consumo del budget LLM nel giorno (UTC) <paramref name="Day"/> e nel suo mese.</summary>
public sealed record LlmBudgetUsage(DateOnly Day, LlmBudgetSpend Today, LlmBudgetSpend Month, bool IsDegraded);

/// <summary>#4169: una spesa in USD, in totale e per classe di budget.</summary>
public sealed record LlmBudgetSpend(decimal TotalUsd, decimal AnswersUsd, decimal BackgroundUsd, decimal EvaluationsUsd);
