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
}
