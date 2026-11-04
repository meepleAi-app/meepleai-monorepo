using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor;

/// <summary>
/// #4164 (ADR-095): stima e tetto di spesa dell'estrattore partono dallo stesso listino del costo
/// reale delle chiamate, non dalle costanti 0,14/0,28 $ di prima.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class MechanicExtractorPricingTests
{
    private static ILlmCostCalculator Calculator(params LlmModelPricing[] catalog) =>
        new LlmCostCalculator(new FakePriceList(catalog), NullLogger<LlmCostCalculator>.Instance);

    [Fact]
    public async Task Resolve_UsesTheCatalogPrice()
    {
        var calculator = Calculator(new LlmModelPricing
        {
            ModelId = "deepseek-chat", Provider = "DeepSeek", InputCostPer1M = 0.30m, OutputCostPer1M = 1.20m
        });

        var (input, output) = await MechanicExtractorPricing.ResolveAsync(calculator, "DeepSeek", "deepseek-chat", CancellationToken.None);

        input.Should().Be(0.30m);
        output.Should().Be(1.20m);
    }

    [Fact]
    public async Task Resolve_SelfHostedModel_IsFree()
    {
        var (input, output) = await MechanicExtractorPricing.ResolveAsync(Calculator(), "Ollama", "llama3:8b", CancellationToken.None);

        input.Should().Be(0m);
        output.Should().Be(0m);
    }

    [Fact]
    public async Task Resolve_UnpricedModel_RefusesTheAnalysis()
    {
        var act = () => MechanicExtractorPricing.ResolveAsync(Calculator(), "OpenRouter", "anthropic/claude-sonnet-4.5", CancellationToken.None);

        (await act.Should().ThrowAsync<ConflictException>())
            .Which.Message.Should().Contain("anthropic/claude-sonnet-4.5");
    }

    private sealed class FakePriceList(IEnumerable<LlmModelPricing> prices) : IModelPriceList
    {
        private readonly Dictionary<string, LlmModelPricing> _prices =
            prices.ToDictionary(p => p.ModelId, StringComparer.Ordinal);

        public LlmModelPricing? Find(string modelId) => _prices.GetValueOrDefault(modelId);

        public Task EnsureLoadedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
