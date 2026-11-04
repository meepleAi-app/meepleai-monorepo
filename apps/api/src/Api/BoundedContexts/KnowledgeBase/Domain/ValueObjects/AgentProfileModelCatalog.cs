namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Ciò che la pubblicazione del profilo deve sapere del catalogo modelli, che l'aggregato non legge:
/// quali modelli hanno un prezzo (ADR-095 D5) e quali supportano il tool calling (spec 2026-10-11 §3.2).
/// Chi pubblica lo legge dal catalogo in quel momento e lo passa, confronto esatto sugli id.
/// </summary>
/// <param name="PricedModels">Gli id dei modelli con un prezzo che il registro dei costi sa usare.</param>
/// <param name="ToolCallingModels">Gli id dei modelli che dichiarano il tool calling.</param>
public sealed record AgentProfileModelCatalog(IReadOnlySet<string> PricedModels, IReadOnlySet<string> ToolCallingModels);
