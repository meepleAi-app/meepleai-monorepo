namespace Api.BoundedContexts.KnowledgeBase.Domain.Models;

/// <summary>
/// #4166 (ADR-095 D6): la spesa registrata in un giorno da una sorgente, in USD.
/// </summary>
/// <param name="RequestSource">Il nome della sorgente, come la scrive il registro costi.</param>
/// <param name="Day">Il giorno della richiesta (UTC).</param>
/// <param name="TotalCostUsd">La somma dei costi registrati.</param>
public sealed record LlmSpendBySourceAndDay(string RequestSource, DateOnly Day, decimal TotalCostUsd);
