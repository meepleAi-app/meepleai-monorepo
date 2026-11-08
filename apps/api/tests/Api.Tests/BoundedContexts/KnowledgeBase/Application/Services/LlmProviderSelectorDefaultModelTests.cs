using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.LlmManagement;
using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Configuration;
using Api.Services.LlmClients;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// #4179: il modello inviato a un provider quando il catalogo non ne ha uno attivo per lui.
/// </summary>
/// <remarks>
/// È il caso di DeepSeek raggiunto come fallback: il seed lascia disattivate le righe DeepSeek, e il
/// ramo <c>_ =&gt;</c> di <c>GetHardcodedDefaultModel</c> gli mandava il modello di Ollama.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4179")]
public sealed class LlmProviderSelectorDefaultModelTests
{
    private readonly Mock<IAiModelConfigurationRepository> _catalog = new();

    private LlmProviderSelector CreateSut(params AiModelConfiguration[] activeModels)
    {
        _catalog.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>())).ReturnsAsync(activeModels);
        var client = new Mock<ILlmClient>();
        client.Setup(c => c.ProviderName).Returns("DeepSeek");
        var circuitBreakers = new Mock<ICircuitBreakerRegistry>();
        circuitBreakers.Setup(r => r.AllowsRequests(It.IsAny<string>())).Returns(true);

        return new LlmProviderSelector(
            [client.Object],
            new Mock<ILlmRoutingStrategy>().Object,
            circuitBreakers.Object,
            Options.Create(new AiProviderSettings()),
            _catalog.Object,
            NullLogger<LlmProviderSelector>.Instance);
    }

    private static AiModelConfiguration Active(string modelId, string provider) =>
        AiModelConfiguration.Create(modelId, modelId, provider, priority: 1,
            new ModelSettings(4096, 0.7m, ModelPricing.Free), isActive: true);

    [Fact]
    public async Task DeepSeek_WithoutActiveCatalogRows_GetsDeepSeekFlash_NotTheOllamaModel()
    {
        var sut = CreateSut(Active("meta-llama/llama-3.3-70b-instruct", "OpenRouter"));

        var model = await sut.GetDefaultModelForProviderAsync("DeepSeek", TestContext.Current.CancellationToken);

        model.Should().Be("deepseek-flash");
        model.Should().NotBe(AgentDefaults.OllamaFallbackModel);
    }

    [Fact]
    public async Task DeepSeek_WithAnActiveCatalogRow_GetsThatRow()
    {
        var sut = CreateSut(Active("deepseek-reasoner", "DeepSeek"));

        var model = await sut.GetDefaultModelForProviderAsync("DeepSeek", TestContext.Current.CancellationToken);

        model.Should().Be("deepseek-reasoner");
    }

    [Fact]
    public async Task EveryProviderWithAClient_HasItsOwnDefault()
    {
        var sut = CreateSut();
        var ct = TestContext.Current.CancellationToken;

        (await sut.GetDefaultModelForProviderAsync("Ollama", ct)).Should().Be(AgentDefaults.OllamaFallbackModel);
        (await sut.GetDefaultModelForProviderAsync("OpenRouter", ct)).Should().Be(AgentDefaults.DefaultModel);
        (await sut.GetDefaultModelForProviderAsync("DeepSeek", ct)).Should().Be("deepseek-flash");
    }

    [Fact]
    public async Task AnUnknownProvider_IsRejected_InsteadOfReceivingAnotherProvidersModel()
    {
        var sut = CreateSut();

        var act = () => sut.GetDefaultModelForProviderAsync("Anthropic", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Anthropic*");
    }
}
