namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Spec 2026-10-11 §3.2: i modelli del catalogo che supportano il tool calling. KnowledgeBase lo chiede
/// per due motivi: rifiutare la pubblicazione di un profilo con l'assistente su un modello senza
/// strumenti, e ricontrollarlo a ogni turno (una migrazione di dati scavalca l'aggregato).
/// </summary>
internal interface IToolCallingModelCatalog
{
    /// <summary>Gli id dei modelli con tool calling, attivi o no: la capacità è del modello, non della sua abilitazione.</summary>
    Task<IReadOnlySet<string>> GetToolCallingModelIdsAsync(CancellationToken cancellationToken);
}
