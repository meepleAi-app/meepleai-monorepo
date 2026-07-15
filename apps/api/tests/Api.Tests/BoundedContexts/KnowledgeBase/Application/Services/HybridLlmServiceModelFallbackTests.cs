using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.Configuration;
using Api.Services;
using Api.Services.LlmClients;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Issue #2961: multi-provider fallback for the explicit-model path used by the Mechanic
/// Extractor. Unlike <see cref="HybridLlmService.GenerateCompletionWithModelAsync"/>
/// (single-shot, #4332 ensemble), <c>GenerateCompletionWithModelFallbackAsync</c> must, on a
/// hard failure of the preferred provider (e.g. DeepSeek 402 credit-exhausted), fall back to the
/// next available provider's default model via the DB-driven fallback chain
/// (<see cref="LlmProviderSelector"/>), until one succeeds or the chain is exhausted.
/// The selector is exercised for real (mocked deps) so the reuse — not a re-implementation — of
/// the existing fallback engine is verified end-to-end.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "2961")]
public sealed class HybridLlmServiceModelFallbackTests
{
    private readonly Mock<ILlmClient> _deepSeekMock = new();
    private readonly Mock<ILlmClient> _openRouterMock = new();
    private readonly Mock<ILlmRoutingStrategy> _routingStrategyMock = new();
    private readonly Mock<IAiModelConfigurationRepository> _modelConfigMock = new();
    private readonly Mock<ICircuitBreakerRegistry> _circuitBreakerRegistryMock = new();
    private readonly Mock<ILlmCostService> _costServiceMock = new();
    private readonly ILogger<HybridLlmService> _logger;
    private readonly ILogger<LlmProviderSelector> _selectorLogger;

    private const string PreferredModel = "deepseek-chat";

    public HybridLlmServiceModelFallbackTests()
    {
        var loggerFactory = new LoggerFactory();
        _logger = loggerFactory.CreateLogger<HybridLlmService>();
        _selectorLogger = loggerFactory.CreateLogger<LlmProviderSelector>();

        // DeepSeek is the preferred provider (owns the explicit model).
        _deepSeekMock.Setup(c => c.ProviderName).Returns("DeepSeek");
        _deepSeekMock.Setup(c => c.SupportsModel(PreferredModel)).Returns(true);

        // OpenRouter is the fallback provider (reached via the selector's fallback chain).
        _openRouterMock.Setup(c => c.ProviderName).Returns("OpenRouter");
        _openRouterMock.Setup(c => c.SupportsModel(It.IsAny<string>())).Returns(true);

        // Empty DB config → selector uses the configured FallbackChain + hardcoded default models.
        _modelConfigMock
            .Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Api.BoundedContexts.SystemConfiguration.Domain.Entities.AiModelConfiguration>());

        _circuitBreakerRegistryMock.Setup(r => r.AllowsRequests(It.IsAny<string>())).Returns(true);
        _circuitBreakerRegistryMock.Setup(r => r.GetState(It.IsAny<string>())).Returns(CircuitState.Closed);
    }

    private ILlmService CreateSut()
    {
        var clients = new[] { _deepSeekMock.Object, _openRouterMock.Object };
        var aiSettings = Options.Create(new AiProviderSettings
        {
            PreferredProvider = "",
            Providers = new Dictionary<string, ProviderConfig>
            {
                ["DeepSeek"] = new() { Enabled = true, BaseUrl = "https://api.deepseek.com", Models = [PreferredModel] },
                ["OpenRouter"] = new() { Enabled = true, BaseUrl = "https://openrouter.ai/api/v1", Models = ["fallback-model"] }
            },
            FallbackChain = ["DeepSeek", "OpenRouter"]
        });

        var selector = new LlmProviderSelector(
            clients,
            _routingStrategyMock.Object,
            _circuitBreakerRegistryMock.Object,
            aiSettings,
            _modelConfigMock.Object,
            _selectorLogger);

        return new HybridLlmService(
            clients,
            selector,
            _circuitBreakerRegistryMock.Object,
            _costServiceMock.Object,
            _logger);
    }

    private static LlmCompletionResult SuccessFrom(string provider) =>
        LlmCompletionResult.CreateSuccess(
            $"ok from {provider}",
            new LlmUsage(10, 5, 15),
            new LlmCost { InputCost = 0m, OutputCost = 0m, ModelId = "m", Provider = provider });

    [Fact]
    public async Task GenerateCompletionWithModelFallbackAsync_PreferredProviderFails_FallsBackToNextProvider()
    {
        // Preferred provider (DeepSeek) hard-fails on the explicit model, e.g. 402 credit exhausted.
        _deepSeekMock
            .Setup(c => c.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateFailure("402 Payment Required"));

        // Fallback provider (OpenRouter) succeeds.
        _openRouterMock
            .Setup(c => c.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessFrom("OpenRouter"));

        var sut = CreateSut();

        var result = await sut.GenerateCompletionWithModelFallbackAsync(
            PreferredModel, "system", "user", RequestSource.Manual, maxTokens: 4000, ct: CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Cost.Provider.Should().Be("OpenRouter");
    }

    [Fact]
    public async Task GenerateCompletionWithModelFallbackAsync_PreferredProviderSucceeds_DoesNotFallBack()
    {
        // Preferred provider succeeds on the first attempt → no fallback provider is consulted.
        _deepSeekMock
            .Setup(c => c.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessFrom("DeepSeek"));

        var sut = CreateSut();

        var result = await sut.GenerateCompletionWithModelFallbackAsync(
            PreferredModel, "system", "user", RequestSource.Manual, maxTokens: 4000, ct: CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Cost.Provider.Should().Be("DeepSeek");
        _openRouterMock.Verify(
            c => c.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GenerateCompletionWithModelFallbackAsync_AllProvidersFail_ReturnsFailure_DoesNotThrow()
    {
        // Every provider hard-fails → the chain is exhausted and an honest failure is returned.
        _deepSeekMock
            .Setup(c => c.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateFailure("402 Payment Required"));
        _openRouterMock
            .Setup(c => c.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateFailure("500 Internal Server Error"));

        var sut = CreateSut();

        var result = await sut.GenerateCompletionWithModelFallbackAsync(
            PreferredModel, "system", "user", RequestSource.Manual, maxTokens: 4000, ct: CancellationToken.None);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task GenerateCompletionWithModelFallbackAsync_NoProviderSupportsModel_ReturnsFailure_WithoutCallingAnyProvider()
    {
        const string UnknownModel = "unknown-model";
        _deepSeekMock.Setup(c => c.SupportsModel(UnknownModel)).Returns(false);
        _openRouterMock.Setup(c => c.SupportsModel(UnknownModel)).Returns(false);

        var sut = CreateSut();

        var result = await sut.GenerateCompletionWithModelFallbackAsync(
            UnknownModel, "system", "user", RequestSource.Manual, maxTokens: 4000, ct: CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No provider supports model");
        _deepSeekMock.Verify(
            c => c.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
