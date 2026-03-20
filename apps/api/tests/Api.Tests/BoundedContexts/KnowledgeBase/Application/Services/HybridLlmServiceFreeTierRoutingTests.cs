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
/// Unit tests for HybridLlmService free-tier routing behaviors (Issues #5087-#5089).
/// Issue #5487: Updated to use ILlmProviderSelector + ICircuitBreakerRegistry.
///
/// Verifies three protection mechanisms:
/// 1. AutomatedTest requests always route to Ollama, bypassing OpenRouter entirely.
/// 2. When RPD quota is known-exhausted, proactively route to Ollama before attempting OpenRouter.
/// 3. OpenRouter rate limit failures are recorded in the quota tracker for future avoidance.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "5089")]
public sealed class HybridLlmServiceFreeTierRoutingTests
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

    public HybridLlmServiceFreeTierRoutingTests()
    {
        var loggerFactory = new LoggerFactory();
        _logger = loggerFactory.CreateLogger<HybridLlmService>();
        _selectorLogger = loggerFactory.CreateLogger<LlmProviderSelector>();

        _ollamaMock.Setup(c => c.ProviderName).Returns("Ollama");
        _ollamaMock.Setup(c => c.SupportsModel(It.IsAny<string>())).Returns(true);

        _openRouterMock.Setup(c => c.ProviderName).Returns("OpenRouter");
        _openRouterMock.Setup(c => c.SupportsModel(It.IsAny<string>())).Returns(true);

        // Model config returns empty → service falls back to hardcoded "llama3.3:70b"
        _modelConfigMock
            .Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Api.BoundedContexts.SystemConfiguration.Domain.Entities.AiModelConfiguration>());

        // Quota tracker defaults: RPD not exhausted, recording is a no-op
        _quotaTrackerMock
            .Setup(t => t.IsRpdExhaustedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _quotaTrackerMock
            .Setup(t => t.RecordRateLimitErrorAsync(
                It.IsAny<string>(), It.IsAny<RateLimitErrorType>(),
                It.IsAny<long?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Circuit breaker registry allows all requests by default
        _circuitBreakerRegistryMock
            .Setup(r => r.AllowsRequests(It.IsAny<string>()))
            .Returns(true);
        _circuitBreakerRegistryMock
            .Setup(r => r.GetState(It.IsAny<string>()))
            .Returns(CircuitState.Closed);
    }

    private HybridLlmService CreateSut()
    {
        var clients = new[] { _ollamaMock.Object, _openRouterMock.Object };
        var aiSettings = Options.Create(new AiProviderSettings
        {
            PreferredProvider = "",
            Providers = new Dictionary<string, ProviderConfig>
            {
                ["Ollama"] = new() { Enabled = true, BaseUrl = "http://meepleai-ollama:11434", Models = [OllamaModel] },
                ["OpenRouter"] = new() { Enabled = true, BaseUrl = "https://openrouter.ai/api/v1", Models = [OpenRouterModel] }
            },
            FallbackChain = ["Ollama", "OpenRouter"]
        });

        var selector = new LlmProviderSelector(
            clients,
            _routingStrategyMock.Object,
            _circuitBreakerRegistryMock.Object,
            aiSettings,
            _modelConfigMock.Object,
            _selectorLogger,
            freeModelQuotaTracker: _quotaTrackerMock.Object);

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

    // ─── AutomatedTest bypass ─────────────────────────────────────────────────

    [Fact]
    public async Task GenerateCompletionAsync_AutomatedTest_NeverCallsOpenRouter()
    {
        _ollamaMock
            .Setup(c => c.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessResult("Ollama"));

        var sut = CreateSut();

        var result = await sut.GenerateCompletionAsync(
            "You are a helpful assistant.", "Say hello",
            source: RequestSource.AutomatedTest);

        result.Success.Should().BeTrue();

        // Routing strategy must NOT be consulted for AutomatedTest requests
        _routingStrategyMock.Verify(
            s => s.SelectProvider(It.IsAny<User?>(), It.IsAny<RagStrategy>(), It.IsAny<string?>(), It.IsAny<string?>()),
            Times.Never);

        // OpenRouter must never receive a request
        _openRouterMock.Verify(
            c => c.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // Ollama must be called exactly once
        _ollamaMock.Verify(
            c => c.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GenerateCompletionAsync_AutomatedTest_NeverChecksRpdQuota()
    {
        _ollamaMock
            .Setup(c => c.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessResult("Ollama"));

        var sut = CreateSut();

        await sut.GenerateCompletionAsync("sys", "user", source: RequestSource.AutomatedTest);

        // IsRpdExhaustedAsync must not be called for AutomatedTest (Ollama bypass happens first)
        _quotaTrackerMock.Verify(
            t => t.IsRpdExhaustedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ─── RPD proactive routing ────────────────────────────────────────────────

    [Fact]
    public async Task GenerateCompletionAsync_RpdExhausted_ProactivelyRoutesToOllama()
    {
        _routingStrategyMock
            .Setup(s => s.SelectProvider(It.IsAny<User?>(), It.IsAny<RagStrategy>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(LlmRoutingDecision.OpenRouter(OpenRouterModel, "free tier selected"));

        _quotaTrackerMock
            .Setup(t => t.IsRpdExhaustedAsync(OpenRouterModel, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true); // Daily quota exhausted

        _ollamaMock
            .Setup(c => c.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessResult("Ollama"));

        var sut = CreateSut();
        var result = await sut.GenerateCompletionAsync("sys", "user");

        result.Success.Should().BeTrue();

        // OpenRouter must never receive the request (proactively skipped)
        _openRouterMock.Verify(
            c => c.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // Ollama is called as the proactive fallback
        _ollamaMock.Verify(
            c => c.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GenerateCompletionAsync_RpdNotExhausted_UsesOpenRouter()
    {
        _routingStrategyMock
            .Setup(s => s.SelectProvider(It.IsAny<User?>(), It.IsAny<RagStrategy>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(LlmRoutingDecision.OpenRouter(OpenRouterModel, "free tier selected"));

        // IsRpdExhausted returns false (default) — OpenRouter should proceed normally
        _openRouterMock
            .Setup(c => c.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessResult("OpenRouter"));

        var sut = CreateSut();
        var result = await sut.GenerateCompletionAsync("sys", "user");

        result.Success.Should().BeTrue();

        _openRouterMock.Verify(
            c => c.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _ollamaMock.Verify(
            c => c.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ─── Rate limit failure recording ─────────────────────────────────────────

    [Fact]
    public async Task GenerateCompletionAsync_OpenRouterRpdFailure_RecordsRpdToQuotaTracker()
    {
        _routingStrategyMock
            .Setup(s => s.SelectProvider(It.IsAny<User?>(), It.IsAny<RagStrategy>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(LlmRoutingDecision.OpenRouter(OpenRouterModel, "free tier selected"));

        var rateLimitMetadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["rate_limit_type"] = "rpd",
            ["rate_limit_reset_ms"] = "1741305600000",
            ["rate_limit_model"] = OpenRouterModel
        };

        _openRouterMock
            .Setup(c => c.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                LlmCompletionResult.CreateFailure("OpenRouter rate limit: Rpd (429)")
                    with
                { Metadata = rateLimitMetadata });

        // Ollama handles the fallback
        _ollamaMock
            .Setup(c => c.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessResult("Ollama"));

        var sut = CreateSut();
        await sut.GenerateCompletionAsync("sys", "user");

        _quotaTrackerMock.Verify(
            t => t.RecordRateLimitErrorAsync(
                OpenRouterModel,
                RateLimitErrorType.Rpd,
                1741305600000L,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GenerateCompletionAsync_OpenRouterRpmFailure_RecordsRpmToQuotaTracker()
    {
        _routingStrategyMock
            .Setup(s => s.SelectProvider(It.IsAny<User?>(), It.IsAny<RagStrategy>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(LlmRoutingDecision.OpenRouter(OpenRouterModel, "free tier selected"));

        var rateLimitMetadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["rate_limit_type"] = "rpm",
            ["rate_limit_model"] = OpenRouterModel
            // No reset timestamp for RPM (transient per-minute limit)
        };

        _openRouterMock
            .Setup(c => c.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                LlmCompletionResult.CreateFailure("OpenRouter rate limit: Rpm (429)")
                    with
                { Metadata = rateLimitMetadata });

        _ollamaMock
            .Setup(c => c.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessResult("Ollama"));

        var sut = CreateSut();
        await sut.GenerateCompletionAsync("sys", "user");

        _quotaTrackerMock.Verify(
            t => t.RecordRateLimitErrorAsync(
                OpenRouterModel,
                RateLimitErrorType.Rpm,
                null, // No reset timestamp supplied for RPM
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GenerateCompletionAsync_OpenRouterFailureWithoutRateLimitMetadata_DoesNotRecordToQuotaTracker()
    {
        _routingStrategyMock
            .Setup(s => s.SelectProvider(It.IsAny<User?>(), It.IsAny<RagStrategy>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(LlmRoutingDecision.OpenRouter(OpenRouterModel, "free tier selected"));

        // Plain failure with no rate limit metadata
        _openRouterMock
            .Setup(c => c.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateFailure("OpenRouter service unavailable"));

        _ollamaMock
            .Setup(c => c.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessResult("Ollama"));

        var sut = CreateSut();
        await sut.GenerateCompletionAsync("sys", "user");

        // No rate limit metadata → quota tracker must not be called
        _quotaTrackerMock.Verify(
            t => t.RecordRateLimitErrorAsync(
                It.IsAny<string>(), It.IsAny<RateLimitErrorType>(),
                It.IsAny<long?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GenerateCompletionAsync_OllamaFailure_DoesNotRecordToQuotaTracker()
    {
        _routingStrategyMock
            .Setup(s => s.SelectProvider(It.IsAny<User?>(), It.IsAny<RagStrategy>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(LlmRoutingDecision.Ollama(OllamaModel, "local routing"));

        _ollamaMock
            .Setup(c => c.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateFailure("Ollama service unavailable"));

        var sut = CreateSut();
        await sut.GenerateCompletionAsync("sys", "user");

        // Rate limit tracking only applies to OpenRouter; Ollama failures must not trigger it
        _quotaTrackerMock.Verify(
            t => t.RecordRateLimitErrorAsync(
                It.IsAny<string>(), It.IsAny<RateLimitErrorType>(),
                It.IsAny<long?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
