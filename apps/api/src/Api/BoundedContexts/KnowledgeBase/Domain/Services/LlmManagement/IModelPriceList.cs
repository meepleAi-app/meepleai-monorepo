using Api.BoundedContexts.KnowledgeBase.Domain.Models;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// #4164 (ADR-095): il listino dei modelli, letto dal catalogo <c>AiModelConfiguration</c>.
/// È l'unica fonte dei prezzi da cui si calcolano costi reali e stimati.
/// </summary>
/// <remarks>
/// La lettura è sincrona perché il calcolo del costo lo è, e lo chiamano i client LLM a ogni
/// risposta: l'implementazione serve un'istantanea in memoria e la ricarica dal database in
/// background.
/// </remarks>
internal interface IModelPriceList
{
    /// <summary>
    /// Prezzo del modello nel catalogo, o <c>null</c> se il catalogo non lo conosce o non è ancora
    /// stato caricato.
    /// </summary>
    LlmModelPricing? Find(string modelId);

    /// <summary>
    /// Se il listino non è mai stato caricato, lo carica ora. Serve a chi può attendere (una
    /// stima prima di una spesa): <see cref="Find"/> su un listino freddo risponde <c>null</c>
    /// mentre il caricamento parte in background, e un modello con prezzo risulterebbe senza.
    /// </summary>
    Task EnsureLoadedAsync(CancellationToken cancellationToken);
}
