using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
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
/// Combined failure scenario tests extending resilience coverage (Issue #5).
/// Tests multi-service degradation paths not covered by single-failure chaos tests.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "5")]
public sealed class LlmResilienceCombinedFailureTests
{
    private readonly Mock<ILlmClient> _ollamaMock = new();
    private readonly Mock<ILlmClient> _openRouterMock = new();
    private readonly Mock<ILlmRoutingStrategy> _routingStrategyMock = new();
    private readonly Mock<IAiModelConfigurationRepository> _modelConfigMock = new();
    private readonly Mock<IFreeModelQuotaTracker> _quotaTrackerMock = new();
    private readonly Mock<ICircuitBreakerRegistry> _circuitBreakerRegistryMock = new();
    private readonly Mock<ILlmCostService> _costServiceMock = new();
    private readonly ILogger<HybridLlmService> _logger;
    private readonly ILogger<LlmProviderSelector> _selectorLogger;

    private const string OllamaModel = "llama3.3:70b";
    private const string OpenRouterModel = "meta-llama/llama-3.3-70b-instruct:free";

    public LlmResilienceCombinedFailureTests()
    {
        var loggerFactory = new LoggerFactory();
        _logger = loggerFactory.CreateLogger<HybridLlmService>();
        _selectorLogger = loggerFactory.CreateLogger<LlmProviderSelector>();

        _ollamaMock.Setup(c => c.ProviderName).Returns("Ollama");
        _ollamaMock.Setup(c => c.SupportsModel(It.IsAny<string>())).Returns(true);

        _openRouterMock.Setup(c => c.ProviderName).Returns("OpenRouter");
        _openRouterMock.Setup(c => c.SupportsModel(It.IsAny<string>())).Returns(true);

        _modelConfigMock
            .Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Api.BoundedContexts.SystemConfiguration.Domain.Entities.AiModelConfiguration>());

        _quotaTrackerMock
            .Setup(t => t.IsRpdExhaustedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _quotaTrackerMock
            .Setup(t => t.RecordRateLimitErrorAsync(
                It.IsAny<string>(), It.IsAny<RateLimitErrorType>(),
                It.IsAny<long?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _circuitBreakerRegistryMock
            .Setup(r => r.AllowsRequests(It.IsAny<string>()))
            .Returns(true);
        _circuitBreakerRegistryMock
            .Setup(r => r.GetState(It.IsAny<string>()))
            .Returns(CircuitState.Closed);
    }

    private IOptions<AiProviderSettings> CreateDefaultAiSettings() =>
        Options.Create(new AiProviderSettings
        {
            PreferredProvider = "",
            Providers = new Dictionary<string, ProviderConfig>
            {
                ["Ollama"] = new() { Enabled = true, BaseUrl = "http://meepleai-ollama:11434", Models = [OllamaModel] },
                ["OpenRouter"] = new() { Enabled = true, BaseUrl = "https://openrouter.ai/api/v1", Models = [OpenRouterModel] }
            },
            FallbackChain = ["Ollama", "OpenRouter"]
        });

    private HybridLlmService CreateSut(IEmergencyOverrideService? emergencyOverrideService = null)
    {
        var clients = new[] { _ollamaMock.Object, _openRouterMock.Object };
        var aiSettings = CreateDefaultAiSettings();

        var selector = new LlmProviderSelector(
            clients,
            _routingStrategyMock.Object,
            _circuitBreakerRegistryMock.Object,
            aiSettings,
            _modelConfigMock.Object,
            _selectorLogger,
            freeModelQuotaTracker: _quotaTrackerMock.Object,
            emergencyOverrideService: emergencyOverrideService);

        return new HybridLlmService(
            clients,
            selector,
            _circuitBreakerRegistryMock.Object,
            _costServiceMock.Object,
            _logger);
    }

    private static LlmCompletionResult SuccessResult(string provider) =>
        LlmCompletionResult.CreateSuccess(
            $"Hello from {provider}!",
            new LlmUsage(10, 5, 15),
            new LlmCost { InputCost = 0m, OutputCost = 0m, ModelId = "test-model", Provider = provider },
            new Dictionary<string, string>(StringComparer.Ordinal) { ["provider"] = provider });

    // ─── Scenario 1: RPD exhausted + Circuit breaker open → Ollama ──────────────

    /// <summary>
    /// COMBINED: OpenRouter RPD quota exhausted AND circuit breaker open simultaneously.
    /// Failure mode: Double degradation — quota depletion combined with circuit protection.
    /// Expected recovery: Selector proactively routes to Ollama without ever calling OpenRouter.
    /// </summary>
    [Fact]
    public async Task RpdExhausted_AND_CircuitBreakerOpen_FallsBackToOllama()
    {
        // Routing strategy nominates OpenRouter
        _routingStrategyMock
            .Setup(s => s.SelectProvider(It.IsAny<User?>(), It.IsAny<RagStrategy>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(LlmRoutingDecision.OpenRouter(OpenRouterModel, "default routing"));

        // RPD quota is exhausted for the OpenRouter model
        _quotaTrackerMock
            .Setup(t => t.IsRpdExhaustedAsync(OpenRouterModel, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Circuit breaker is also open for OpenRouter
        _circuitBreakerRegistryMock
            .Setup(r => r.AllowsRequests("OpenRouter"))
            .Returns(false);
        _circuitBreakerRegistryMock
            .Setup(r => r.GetState("OpenRouter"))
            .Returns(CircuitState.Open);

        // Ollama is healthy and responds successfully
        _ollamaMock
            .Setup(c => c.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessResult("Ollama"));

        var sut = CreateSut();
        var result = await sut.GenerateCompletionAsync("sys", "user");

        result.Success.Should().BeTrue();

        // OpenRouter must never be called — proactive rerouting should prevent any attempt
        _openRouterMock.Verify(
            c => c.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ─── Scenario 2: OpenRouter 429 + Ollama fallback → rate limit recorded ─────

    /// <summary>
    /// COMBINED: OpenRouter throws 429, Ollama handles fallback, and the rate limit error
    /// is recorded via the circuit breaker registry (RecordFailure path).
    /// Failure mode: Rate limit burst with successful fallback.
    /// Expected recovery: Request succeeds via Ollama; circuit breaker records the OpenRouter failure.
    /// </summary>
    [Fact]
    public async Task OpenRouter429_AND_OllamaAvailable_RecordsRateLimitError()
    {
        // Routing strategy selects OpenRouter
        _routingStrategyMock
            .Setup(s => s.SelectProvider(It.IsAny<User?>(), It.IsAny<RagStrategy>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(LlmRoutingDecision.OpenRouter(OpenRouterModel, "default routing"));

        // OpenRouter throws 429
        _openRouterMock
            .Setup(c => c.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("429 Too Many Requests"));

        // Ollama succeeds as fallback
        _ollamaMock
            .Setup(c => c.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessResult("Ollama"));

        var sut = CreateSut();
        var result = await sut.GenerateCompletionAsync("sys", "user");

        // Request must succeed via Ollama fallback
        result.Success.Should().BeTrue();

        // Circuit breaker registry must have recorded the OpenRouter failure
        _circuitBreakerRegistryMock.Verify(
            r => r.RecordFailure("OpenRouter", It.IsAny<long>()),
            Times.Once);
    }

    // ─── Scenario 3: Success resets counter after intermittent failures ──────────

    /// <summary>
    /// COMBINED: Intermittent failures (4) followed by a success that resets the counter,
    /// then 4 more failures — circuit must remain Closed because the success reset prevented
    /// reaching the threshold of 5 consecutive failures.
    /// Failure mode: Flaky provider with intermittent errors.
    /// Expected recovery: Success resets failure counter, preventing false circuit opens.
    /// </summary>
    [Fact]
    public void CircuitBreaker_SuccessResetsCounter_AfterIntermittentFailures()
    {
        var breaker = new CircuitBreakerState();

        // 4 failures — just below threshold of 5
        for (int i = 0; i < 4; i++)
            breaker.RecordFailure();

        breaker.State.Should().Be(CircuitState.Closed);
        breaker.ConsecutiveFailures.Should().Be(4);

        // 1 success resets the consecutive failure counter to 0
        breaker.RecordSuccess();
        breaker.ConsecutiveFailures.Should().Be(0);
        breaker.State.Should().Be(CircuitState.Closed);

        // 4 more failures — total historical failures = 8, but consecutive = 4 (below threshold)
        for (int i = 0; i < 4; i++)
            breaker.RecordFailure();

        breaker.State.Should().Be(CircuitState.Closed);
        breaker.ConsecutiveFailures.Should().Be(4);
        breaker.AllowsRequests().Should().BeTrue();
    }

    // ─── Scenario 4: Circuit open bypasses OpenRouter, routes to Ollama ─────────

    /// <summary>
    /// COMBINED: Circuit breaker is open for OpenRouter. Even though routing strategy selects
    /// OpenRouter, the selector must detect the open circuit and fall back to Ollama.
    /// Failure mode: Provider circuit tripped due to prior failures.
    /// Expected recovery: Selector transparently reroutes to Ollama; OpenRouter never called.
    /// </summary>
    [Fact]
    public async Task OpenRouterCircuitOpen_RoutingBypassesOpenRouter_UsesOllama()
    {
        // Routing strategy nominates OpenRouter
        _routingStrategyMock
            .Setup(s => s.SelectProvider(It.IsAny<User?>(), It.IsAny<RagStrategy>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(LlmRoutingDecision.OpenRouter(OpenRouterModel, "default routing"));

        // Circuit breaker is open for OpenRouter — requests not allowed
        _circuitBreakerRegistryMock
            .Setup(r => r.AllowsRequests("OpenRouter"))
            .Returns(false);
        _circuitBreakerRegistryMock
            .Setup(r => r.GetState("OpenRouter"))
            .Returns(CircuitState.Open);

        // Ollama circuit is healthy
        _circuitBreakerRegistryMock
            .Setup(r => r.AllowsRequests("Ollama"))
            .Returns(true);
        _circuitBreakerRegistryMock
            .Setup(r => r.GetState("Ollama"))
            .Returns(CircuitState.Closed);

        // Ollama succeeds
        _ollamaMock
            .Setup(c => c.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessResult("Ollama"));

        var sut = CreateSut();
        var result = await sut.GenerateCompletionAsync("sys", "user");

        result.Success.Should().BeTrue();

        // OpenRouter must never be called — circuit breaker should prevent any attempt
        _openRouterMock.Verify(
            c => c.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
