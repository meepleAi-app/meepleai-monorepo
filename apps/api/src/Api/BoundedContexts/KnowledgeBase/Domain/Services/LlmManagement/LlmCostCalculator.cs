using System.Diagnostics;
using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.Observability;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Domain service for calculating LLM request costs based on token usage
/// ISSUE-960: BGAI-018 - Financial cost tracking implementation
/// </summary>
/// <remarks>
/// #4164 (ADR-095): i prezzi vengono dal catalogo modelli tramite <see cref="IModelPriceList"/>.
/// Il listino statico che stava qui («as of 2026-01-16») divergeva dal catalogo, e un modello
/// assente da entrambi veniva registrato come gratuito senza che nulla lo segnalasse.
///
/// Tre esiti, riportati in <see cref="LlmCostCalculation.PricingStatus"/>:
/// - <see cref="LlmPricingStatus.Priced"/>: il catalogo ha il prezzo;
/// - <see cref="LlmPricingStatus.Free"/>: il catalogo lo dà a zero, oppure il modello gira in casa
///   (Ollama) e non ha un costo per token;
/// - <see cref="LlmPricingStatus.Unpriced"/>: il catalogo non lo conosce. Il costo resta zero perché
///   le colonne del registro sono obbligatorie, ma l'esito è un errore nei log e incrementa
///   <c>meepleai_llm_unpriced_calls_total</c>. Il rifiuto della chiamata arriva con il gateway
///   (ADR-095 fetta 3).
///
/// Il prezzo della lettura dalla cache sta nel catalogo, ma qui non si usa ancora: i client non
/// riportano i token letti dalla cache, quindi ogni token d'ingresso si paga al prezzo pieno, che è
/// il limite superiore.
/// </remarks>
internal sealed class LlmCostCalculator : ILlmCostCalculator
{
    private const string SelfHostedProvider = "Ollama";

    private readonly IModelPriceList _priceList;
    private readonly ILogger<LlmCostCalculator> _logger;

    public LlmCostCalculator(IModelPriceList priceList, ILogger<LlmCostCalculator> logger)
    {
        _priceList = priceList ?? throw new ArgumentNullException(nameof(priceList));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public LlmCostCalculation CalculateCost(
        string modelId,
        string provider,
        int promptTokens,
        int completionTokens)
    {
        if (promptTokens < 0 || completionTokens < 0)
        {
            _logger.LogWarning("Invalid token counts: prompt={Prompt}, completion={Completion}",
                promptTokens, completionTokens);
            return LlmCostCalculation.Empty;
        }

        var pricing = ResolvePricing(modelId, provider);
        LlmPricingStatus status;
        if (pricing is not null)
        {
            status = pricing.IsFree ? LlmPricingStatus.Free : LlmPricingStatus.Priced;
        }
        else
        {
            status = LlmPricingStatus.Unpriced;
            pricing = LlmModelPricing.Free with { ModelId = modelId, Provider = provider };
            _logger.LogError(
                "Model {ModelId} (provider {Provider}) has no price in the AI model catalog: the call is logged at $0 with pricing status Unpriced. Add the model to the catalog",
                modelId, provider);
            MeepleAiMetrics.LlmUnpricedCalls.Add(1, new TagList
            {
                { "provider", provider },
                { "model", modelId }
            });
        }

        // Calculate costs (tokens / 1M * cost_per_1M)
        var inputCost = (promptTokens / 1_000_000m) * pricing.InputCostPer1M;
        var outputCost = (completionTokens / 1_000_000m) * pricing.OutputCostPer1M;

        var calculation = new LlmCostCalculation
        {
            ModelId = modelId,
            Provider = provider,
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
            InputCost = Math.Round(inputCost, 6),  // Round to 6 decimal places (micro-dollars)
            OutputCost = Math.Round(outputCost, 6),
            PricingStatus = status
        };

        _logger.LogDebug(
            "Cost calculated for {Model}: {Tokens} tokens = ${Cost:F6} (input: ${InputCost:F6}, output: ${OutputCost:F6}, pricing: {PricingStatus})",
            modelId, calculation.TotalTokens, calculation.TotalCost, calculation.InputCost, calculation.OutputCost, status);

        return calculation;
    }

    /// <inheritdoc/>
    public LlmModelPricing? GetModelPricing(string modelId) => _priceList.Find(modelId);

    /// <inheritdoc/>
    public LlmModelPricing? ResolvePricing(string modelId, string provider)
    {
        var pricing = _priceList.Find(modelId);
        if (pricing is not null)
        {
            return pricing;
        }

        return string.Equals(provider, SelfHostedProvider, StringComparison.OrdinalIgnoreCase)
            ? LlmModelPricing.Free with { ModelId = modelId, Provider = provider }
            : null;
    }
}
