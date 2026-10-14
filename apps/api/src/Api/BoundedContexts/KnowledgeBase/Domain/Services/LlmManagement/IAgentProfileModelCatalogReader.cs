using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Ciò che la pubblicazione del profilo deve sapere del catalogo modelli (#4169 6c): quali modelli
/// hanno un prezzo (ADR-095 D5) e quali il tool calling (spec 2026-10-11 §3.2), letti insieme.
/// </summary>
internal interface IAgentProfileModelCatalogReader
{
    /// <summary>Il catalogo com'è adesso: un prezzo appena impostato dall'admin vale subito.</summary>
    Task<AgentProfileModelCatalog> ReadAsync(CancellationToken cancellationToken);
}
