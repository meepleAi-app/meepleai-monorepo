using System.Diagnostics.Metrics;
using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Observability;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// #4164 (ADR-095 fetta 1): il costo di una chiamata si calcola dal catalogo modelli
/// (<see cref="IModelPriceList"/>), non da un listino scritto nel codice. Sostituisce i test del
/// listino statico «as of 2026-01-16».
/// </summary>
/// <remarks>
/// I due test «CatalogOnly» e «CatalogPriceDiffersFromCode» sono stati visti fallire contro il
/// calcolatore precedente: il primo dava 0 $ invece di 0,75, il secondo 1,37 $ invece di 1,50.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class LlmCostCalculatorTests
{
    private readonly Mock<ILogger<LlmCostCalculator>> _logger = new();

    private LlmCostCalculator CreateCalculator(params LlmModelPricing[] catalog) =>
        new(new FakePriceList(catalog), _logger.Object);

    private static LlmModelPricing Price(string modelId, string provider, decimal input, decimal output) => new()
    {
        ModelId = modelId,
        Provider = provider,
        InputCostPer1M = input,
        OutputCostPer1M = output
    };

    [Fact]
    public void CalculateCost_ModelOnlyInCatalog_UsesCatalogPrice()
    {
        var calculator = CreateCalculator(Price("openai/gpt-4o-mini", "OpenRouter", 0.15m, 0.60m));

        var result = calculator.CalculateCost("openai/gpt-4o-mini", "OpenRouter", 1_000_000, 1_000_000);

        result.InputCost.Should().Be(0.15m);
        result.OutputCost.Should().Be(0.60m);
        result.TotalCost.Should().Be(0.75m);
        result.PricingStatus.Should().Be(LlmPricingStatus.Priced);
    }

    [Fact]
    public void CalculateCost_CatalogPriceDiffersFromCode_CatalogWins()
    {
        // Il vecchio listino statico dava a deepseek-chat 0,27/1,10 $.
        var calculator = CreateCalculator(Price("deepseek-chat", "DeepSeek", 0.30m, 1.20m));

        var result = calculator.CalculateCost("deepseek-chat", "DeepSeek", 1_000_000, 1_000_000);

        result.TotalCost.Should().Be(1.50m);
    }

    [Fact]
    public void CalculateCost_TracksTokensAndRoundsToMicroDollars()
    {
        var calculator = CreateCalculator(Price("m", "OpenRouter", 0.123456789m, 0.987654321m));

        var result = calculator.CalculateCost("m", "OpenRouter", 1234, 567);

        result.ModelId.Should().Be("m");
        result.Provider.Should().Be("OpenRouter");
        result.PromptTokens.Should().Be(1234);
        result.CompletionTokens.Should().Be(567);
        result.TotalTokens.Should().Be(1801);
        result.InputCost.Should().Be(0.000152m);   // 1234 / 1M * 0.123456789 = 0.00015234…
        result.OutputCost.Should().Be(0.00056m);   // 567 / 1M * 0.987654321 = 0.00055999…
    }

    [Fact]
    public void CalculateCost_FreeInCatalog_IsFreeNotUnpriced()
    {
        var calculator = CreateCalculator(Price("meta-llama/llama-3.3-70b-instruct:free", "OpenRouter", 0m, 0m));

        var result = calculator.CalculateCost("meta-llama/llama-3.3-70b-instruct:free", "OpenRouter", 5000, 2000);

        result.TotalCost.Should().Be(0m);
        result.PricingStatus.Should().Be(LlmPricingStatus.Free);
        VerifyNoErrorLogged();
    }

    [Fact]
    public void CalculateCost_SelfHostedModelAbsentFromCatalog_IsFreeWithoutError()
    {
        var calculator = CreateCalculator();

        var result = calculator.CalculateCost("qwen2.5:3b", "Ollama", 5000, 2000);

        result.TotalCost.Should().Be(0m);
        result.PricingStatus.Should().Be(LlmPricingStatus.Free);
        VerifyNoErrorLogged();
    }

    [Fact]
    public void CalculateCost_ModelAbsentFromCatalog_IsUnpricedAndLoggedAsError()
    {
        var calculator = CreateCalculator(Price("deepseek-chat", "DeepSeek", 0.30m, 1.20m));

        var result = calculator.CalculateCost("anthropic/claude-sonnet-4.5", "OpenRouter", 5000, 2000);

        result.TotalCost.Should().Be(0m);
        result.PricingStatus.Should().Be(LlmPricingStatus.Unpriced);
        _logger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("anthropic/claude-sonnet-4.5", StringComparison.Ordinal)),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void CalculateCost_ModelAbsentFromCatalog_IncrementsUnpricedCounter()
    {
        // Il MeterListener vede le misure di tutto il processo: un id di modello univoco isola
        // questo test da chi emette lo stesso contatore in parallelo.
        var modelId = $"test/unpriced-{Guid.NewGuid():N}";
        long counted = 0;
        using var listener = new MeterListener();
        // Per nome, non per riferimento: lo strumento viene pubblicato mentre la classe statica
        // si inizializza, quando il campo LlmUnpricedCalls non è ancora assegnato.
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == MeepleAiMetrics.MeterName && instrument.Name == "meepleai.llm.unpriced")
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            foreach (var tag in tags)
            {
                if (tag.Key == "model" && Equals(tag.Value, modelId))
                {
                    Interlocked.Add(ref counted, value);
                }
            }
        });
        listener.Start();

        var calculator = CreateCalculator();
        calculator.CalculateCost(modelId, "OpenRouter", 10, 10);
        calculator.CalculateCost(modelId, "OpenRouter", 10, 10);

        Interlocked.Read(ref counted).Should().Be(2);
    }

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(10, -1)]
    public void CalculateCost_NegativeTokens_ReturnsEmpty(int prompt, int completion)
    {
        var calculator = CreateCalculator(Price("m", "OpenRouter", 1m, 1m));

        calculator.CalculateCost("m", "OpenRouter", prompt, completion).Should().Be(LlmCostCalculation.Empty);
    }

    [Fact]
    public void GetModelPricing_ReadsOnlyTheCatalog()
    {
        var calculator = CreateCalculator(Price("deepseek-chat", "DeepSeek", 0.30m, 1.20m));

        calculator.GetModelPricing("deepseek-chat")!.InputCostPer1M.Should().Be(0.30m);
        calculator.GetModelPricing("llama3:8b").Should().BeNull("il catalogo non lo elenca, anche se è un modello in casa");
    }

    [Theory]
    [InlineData("deepseek-chat", "DeepSeek", 0.30)]
    [InlineData("llama3:8b", "Ollama", 0.0)]
    public async Task ResolvePricingAsync_CatalogOrSelfHosted(string modelId, string provider, double expectedInput)
    {
        var calculator = CreateCalculator(Price("deepseek-chat", "DeepSeek", 0.30m, 1.20m));

        var pricing = await calculator.ResolvePricingAsync(modelId, provider);

        pricing!.InputCostPer1M.Should().Be((decimal)expectedInput);
    }

    [Fact]
    public async Task ResolvePricingAsync_UnpricedModel_ReturnsNull()
    {
        (await CreateCalculator().ResolvePricingAsync("anthropic/claude-sonnet-4.5", "OpenRouter")).Should().BeNull();
    }

    private void VerifyNoErrorLogged() =>
        _logger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);

    private sealed class FakePriceList(IEnumerable<LlmModelPricing> prices) : IModelPriceList
    {
        private readonly Dictionary<string, LlmModelPricing> _prices =
            prices.ToDictionary(p => p.ModelId, StringComparer.Ordinal);

        public LlmModelPricing? Find(string modelId) => _prices.GetValueOrDefault(modelId);

        public Task EnsureLoadedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
