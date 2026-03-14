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

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Chaos/resilience tests for LLM subsystem failure paths (Issue #5482).
/// Issue #5487: Updated to use ILlmProviderSelector + ICircuitBreakerRegistry.
///
/// Tests verify correct behavior under simulated failure scenarios:
/// 1. Circuit breaker opens after consecutive failures → fallback activates
/// 2. OpenRouter 429 burst → fallback to Ollama
/// 3. All providers down → meaningful error returned (not unhandled exception)
/// 4. Circuit breaker half-open → probe success → recovery to closed
/// 5. Circuit breaker half-open → probe failure → reopen
/// 6. Redis unavailable → health monitor reports degraded gracefully
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "5482")]
public sealed class LlmResilienceChaosTests
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

    public LlmResilienceChaosTests()
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

        // Circuit breaker registry allows all requests by default
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

    // ─── Scenario 1: Circuit breaker opens after consecutive failures ──────────

    /// <summary>
    /// CHAOS: Circuit breaker transitions from Closed → Open after 5 consecutive failures.
    /// Failure mode: Provider returns errors repeatedly.
    /// Expected recovery: Circuit opens, blocking further requests to failed provider.
    /// </summary>
    [Fact]
    public void CircuitBreaker_OpensAfter5ConsecutiveFailures()
    {
        var breaker = new CircuitBreakerState();

        for (int i = 0; i < 5; i++)
            breaker.RecordFailure();

        Assert.Equal(CircuitState.Open, breaker.State);
        Assert.Equal(5, breaker.ConsecutiveFailures);
        Assert.False(breaker.AllowsRequests());
    }

    /// <summary>
    /// CHAOS: Circuit stays closed when failures are below threshold.
    /// Failure mode: Intermittent provider errors (4 failures, then success resets).
    /// Expected recovery: Circuit remains closed, no false positives.
    /// </summary>
    [Fact]
    public void CircuitBreaker_StaysClosedBelowThreshold()
    {
        var breaker = new CircuitBreakerState();

        for (int i = 0; i < 4; i++)
            breaker.RecordFailure();

        Assert.Equal(CircuitState.Closed, breaker.State);
        Assert.True(breaker.AllowsRequests());

        breaker.RecordSuccess(); // Reset
        Assert.Equal(0, breaker.ConsecutiveFailures);
    }

    // ─── Scenario 2: OpenRouter 429 burst → verify fallback to Ollama ──────────

    /// <summary>
    /// CHAOS: OpenRouter returns 429 (rate limited), service falls back to Ollama.
    /// Failure mode: OpenRouter rate limit burst.
    /// Expected recovery: Ollama handles the request successfully.
    /// </summary>
    [Fact]
    public async Task OpenRouter429Burst_FallsBackToOllama()
    {
        _routingStrategyMock
            .Setup(s => s.SelectProvider(It.IsAny<User?>(), It.IsAny<RagStrategy>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(LlmRoutingDecision.OpenRouter(OpenRouterModel, "default routing"));

        // OpenRouter throws (simulating 429)
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

        Assert.True(result.Success);
    }

    // ─── Scenario 3: All providers down → meaningful error ─────────────────────

    /// <summary>
    /// CHAOS: Both Ollama and OpenRouter are down simultaneously.
    /// Failure mode: Complete LLM infrastructure failure.
    /// Expected recovery: Returns meaningful failure result, no unhandled exception.
    /// </summary>
    [Fact]
    public async Task AllProvidersDown_ReturnsMeaningfulError()
    {
        _routingStrategyMock
            .Setup(s => s.SelectProvider(It.IsAny<User?>(), It.IsAny<RagStrategy>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(LlmRoutingDecision.OpenRouter(OpenRouterModel, "default routing"));

        // Both providers throw
        _openRouterMock
            .Setup(c => c.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Service unavailable"));

        _ollamaMock
            .Setup(c => c.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var sut = CreateSut();
        var result = await sut.GenerateCompletionAsync("sys", "user");

        // Must return failure, not throw unhandled exception
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    // ─── Scenario 4: Circuit half-open → probe success → recovery ──────────────

    /// <summary>
    /// CHAOS: Circuit breaker transitions Open → HalfOpen → Closed after recovery.
    /// Failure mode: Provider was down, comes back online.
    /// Expected recovery: After 3 consecutive successes in HalfOpen, circuit closes.
    /// </summary>
    [Fact]
    public void CircuitBreaker_HalfOpen_ProbeSuccess_RecoversToClosed()
    {
        var breaker = new CircuitBreakerState();
        var transitions = new List<(CircuitState from, CircuitState to)>();
        breaker.OnStateTransition = (from, to) => transitions.Add((from, to));

        // Open the circuit
        for (int i = 0; i < 5; i++)
            breaker.RecordFailure();

        Assert.Equal(CircuitState.Open, breaker.State);

        // Simulate timeout expiry by using reflection to set OpenedAt in the past
        var openedAtProp = typeof(CircuitBreakerState)
            .GetProperty(nameof(CircuitBreakerState.OpenedAt),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        openedAtProp!.SetValue(breaker, DateTime.UtcNow.AddSeconds(-31));

        // AllowsRequests transitions to HalfOpen
        Assert.True(breaker.AllowsRequests());
        Assert.Equal(CircuitState.HalfOpen, breaker.State);

        // 3 consecutive successes → close
        breaker.RecordSuccess();
        breaker.RecordSuccess();
        breaker.RecordSuccess();

        Assert.Equal(CircuitState.Closed, breaker.State);
        Assert.True(breaker.AllowsRequests());

        // Verify transition chain: Closed→Open→HalfOpen→Closed
        Assert.Contains(transitions, t => t.from == CircuitState.Closed && t.to == CircuitState.Open);
        Assert.Contains(transitions, t => t.from == CircuitState.Open && t.to == CircuitState.HalfOpen);
        Assert.Contains(transitions, t => t.from == CircuitState.HalfOpen && t.to == CircuitState.Closed);
    }

    // ─── Scenario 5: Circuit half-open → probe failure → reopen ────────────────

    /// <summary>
    /// CHAOS: Circuit breaker transitions HalfOpen → Open after probe failure.
    /// Failure mode: Provider recovery attempt fails.
    /// Expected recovery: Circuit reopens, blocking requests again.
    /// </summary>
    [Fact]
    public void CircuitBreaker_HalfOpen_ProbeFailure_Reopens()
    {
        var breaker = new CircuitBreakerState();

        // Open the circuit
        for (int i = 0; i < 5; i++)
            breaker.RecordFailure();

        // Force HalfOpen by setting OpenedAt in the past
        var openedAtProp = typeof(CircuitBreakerState)
            .GetProperty(nameof(CircuitBreakerState.OpenedAt),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        openedAtProp!.SetValue(breaker, DateTime.UtcNow.AddSeconds(-31));
        breaker.AllowsRequests(); // Transition to HalfOpen

        Assert.Equal(CircuitState.HalfOpen, breaker.State);

        // Probe failure → reopen
        breaker.RecordFailure();

        Assert.Equal(CircuitState.Open, breaker.State);
        Assert.False(breaker.AllowsRequests());
    }

    // ─── Scenario 6: Redis unavailable → health monitor degrades gracefully ────

    /// <summary>
    /// CHAOS: Redis is down, health monitor reports degraded status.
    /// Failure mode: Redis connection lost.
    /// Expected recovery: Health check returns Degraded, not Unhealthy or exception.
    /// </summary>
    [Fact]
    public async Task RedisDown_HealthMonitorReportsDegraded()
    {
        var redisMock = new Mock<StackExchange.Redis.IConnectionMultiplexer>();
        var dbMock = new Mock<StackExchange.Redis.IDatabase>();
        redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(dbMock.Object);

        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
        var monitor = new RedisRateLimitingHealthMonitor(
            redisMock.Object,
            new Mock<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>().Object,
            config,
            new Mock<ILogger<RedisRateLimitingHealthMonitor>>().Object);

        // Use reflection to set degraded state (simulating Redis failure detection)
        var prop = typeof(RedisRateLimitingHealthMonitor)
            .GetProperty(
                nameof(RedisRateLimitingHealthMonitor.IsRateLimitingDegraded),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        prop!.GetSetMethod(nonPublic: true)!.Invoke(monitor, [true]);

        var services = new List<Microsoft.Extensions.Hosting.IHostedService> { monitor };
        var healthCheck = new Api.Infrastructure.Health.Checks.RedisRateLimitingHealthCheck(services);

        var result = await healthCheck.CheckHealthAsync(
            new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext(),
            CancellationToken.None);

        Assert.Equal(Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded, result.Status);
        Assert.Contains("rate limiting is disabled", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    // ─── Additional resilience scenarios ───────────────────────────────────────

    /// <summary>
    /// CHAOS: State transition callback fires correctly for circuit breaker events.
    /// Verifies the notification mechanism works under state changes.
    /// </summary>
    [Fact]
    public void CircuitBreaker_StateTransitionCallback_FiresOnAllTransitions()
    {
        var breaker = new CircuitBreakerState();
        var callbackCount = 0;
        breaker.OnStateTransition = (_, _) => callbackCount++;

        // Closed → Open (5 failures)
        for (int i = 0; i < 5; i++)
            breaker.RecordFailure();

        Assert.Equal(1, callbackCount); // Closed→Open

        // Admin reset: Open → Closed
        breaker.Reset();
        Assert.Equal(2, callbackCount); // Open→Closed
    }

    /// <summary>
    /// CHAOS: Emergency override forces Ollama routing when set.
    /// Failure mode: Admin activates force-ollama override.
    /// Expected recovery: All requests route to Ollama regardless of routing strategy.
    /// </summary>
    [Fact]
    public async Task EmergencyOverride_ForceOllama_RoutesToOllama()
    {
        // Routing strategy selects OpenRouter by default
        _routingStrategyMock
            .Setup(s => s.SelectProvider(It.IsAny<User?>(), It.IsAny<RagStrategy>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(LlmRoutingDecision.OpenRouter(OpenRouterModel, "default routing"));

        var emergencyMock = new Mock<IEmergencyOverrideService>();
        emergencyMock
            .Setup(e => e.IsForceOllamaOnlyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _ollamaMock
            .Setup(c => c.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessResult("Ollama"));

        var sut = CreateSut(emergencyOverrideService: emergencyMock.Object);
        var result = await sut.GenerateCompletionAsync("sys", "user");

        Assert.True(result.Success);

        // OpenRouter must not be called
        _openRouterMock.Verify(
            c => c.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// CHAOS: RPD exhaustion on OpenRouter → proactive fallback to Ollama.
    /// Failure mode: OpenRouter daily quota depleted.
    /// Expected recovery: Requests proactively route to Ollama without attempting OpenRouter.
    /// </summary>
    [Fact]
    public async Task RpdExhausted_OpenRouter_ProactivelyFallsBackToOllama()
    {
        _routingStrategyMock
            .Setup(s => s.SelectProvider(It.IsAny<User?>(), It.IsAny<RagStrategy>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(LlmRoutingDecision.OpenRouter(OpenRouterModel, "free tier"));

        _quotaTrackerMock
            .Setup(t => t.IsRpdExhaustedAsync(OpenRouterModel, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _ollamaMock
            .Setup(c => c.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessResult("Ollama"));

        var sut = CreateSut();
        var result = await sut.GenerateCompletionAsync("sys", "user");

        Assert.True(result.Success);

        _openRouterMock.Verify(
            c => c.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
