using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Middleware.Exceptions;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor;

/// <summary>
/// #4164 (ADR-095): i prezzi con cui l'estrattore di meccaniche stima e limita la spesa vengono dal
/// catalogo modelli, lo stesso listino da cui si calcola il costo reale delle chiamate.
/// </summary>
/// <remarks>
/// Sostituisce le costanti 0,14/0,28 $ per milione («DeepSeek list pricing as of 2026-04») che
/// handler ed executor si portavano dietro separatamente. Un modello senza prezzo fa fallire la
/// richiesta: con un prezzo ignoto il tetto di spesa dell'analisi non è applicabile.
/// </remarks>
internal static class MechanicExtractorPricing
{
    public static async Task<(decimal InputCostPerMillion, decimal OutputCostPerMillion)> ResolveAsync(
        ILlmCostCalculator costCalculator,
        string provider,
        string model,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(costCalculator);

        var pricing = await costCalculator.ResolvePricingAsync(model, provider, cancellationToken).ConfigureAwait(false)
            ?? throw new ConflictException(
                $"Model '{model}' (provider {provider}) has no price in the AI model catalog, so the cost cap of a mechanic analysis cannot be enforced. Add the model to the catalog first.");

        return (pricing.InputCostPer1M, pricing.OutputCostPer1M);
    }
}
