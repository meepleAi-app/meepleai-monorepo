using System.Diagnostics;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.LlmManagement;
using Api.BoundedContexts.SystemConfiguration.Domain.Enums;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.Configuration;
using Api.Services;
using Api.Services.LlmClients;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using FluentAssertions;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.Integration;

/// <summary>
/// End-to-end integration tests for adaptive LLM provider switching
/// ISSUE-964 (BGAI-022): Integration tests for LLM provider routing reliability
/// </summary>
/// <remarks>
/// Test Coverage:
/// 1. Ollama primary success
/// 2. Ollama fails → OpenRouter fallback (if configured)
/// 3. Both providers down → error handling
/// 4. Feature flag toggle
/// 5. Cost tracking accuracy
/// 6. Latency comparison
///
/// Dependencies: BGAI-020, BGAI-021
/// </remarks>
[Trait("Category", TestCategories.Integration)]
public class AdaptiveLlmRoutingIntegrationTests : IAsyncLifetime
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<HybridLlmService> _serviceLogger;
    private readonly ILogger<HybridAdaptiveRoutingStrategy> _strategyLogger;
    private readonly Mock<ILlmCostLogRepository> _mockCostLogRepository;
    private readonly Mock<ILlmCostService> _mockCostService;

    // Constants for test configuration
    private const int CircuitBreakerFailureThreshold = 6; // 5 failures + 1 extra to ensure circuit opens
    private const int LatencyCollectionSampleSize = 10;
    private const int MinimumLatencyMs = 100;

    public AdaptiveLlmRoutingIntegrationTests()
    {
        // Setup loggers (use console for visibility during tests)
        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        _serviceLogger = _loggerFactory.CreateLogger<HybridLlmService>();
        _strategyLogger = _loggerFactory.CreateLogger<HybridAdaptiveRoutingStrategy>();

        _mockCostLogRepository = new Mock<ILlmCostLogRepository>();
        _mockCostService = new Mock<ILlmCostService>();
    }

    public ValueTask InitializeAsync()
    {
        // Setup mock cost log repository to always succeed
        _mockCostLogRepository
            .Setup(r => r.LogCostAsync(
                It.IsAny<Guid?>(),
                It.IsAny<string>(),
                It.IsAny<LlmCostCalculation>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<string?>(),
                It.IsAny<int>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _loggerFactory?.Dispose();
        return ValueTask.CompletedTask;
    }
    [Fact]
    public async Task OllamaPrimarySuccess_GeneratesCompletion()
    {
        // Arrange - Force deterministic Ollama routing by setting PreferredProvider
        var aiSettings = CreateAiSettings(
            ollamaEnabled: true,
            openRouterEnabled: true,
            preferredProvider: "Ollama"); // Force Ollama to avoid randomness

        var mockOllamaClient = CreateMockOllamaClient(shouldSucceed: true);
        var mockOpenRouterClient = CreateMockOpenRouterClient(shouldSucceed: true);

        var service = CreateHybridLlmService(aiSettings, mockOllamaClient, mockOpenRouterClient);
        var user = CreateUser(Role.User);

        // Act
        var result = await service.GenerateCompletionAsync(
            "You are a helpful assistant.",
            "Say hello",
            user,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        (result.Success).Should().BeTrue($"Expected success but got failure: {result.ErrorMessage}");
        result.Response.Should().NotBeEmpty();
        result.Cost.Provider.Should().Be("Ollama");
        result.Cost.TotalCost.Should().Be(0m); // Ollama is free

        // Verify Ollama was called exactly once (deterministic)
        mockOllamaClient.Verify(
            c => c.GenerateCompletionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<double>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify OpenRouter was NOT called
        mockOpenRouterClient.Verify(
            c => c.GenerateCompletionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<double>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
    [Fact]
    public async Task OllamaFails_FallsBackToOpenRouter()
    {
        // Arrange
        var aiSettings = CreateAiSettings(
            ollamaEnabled: true,
            openRouterEnabled: true,
            preferredProvider: "",
            fallbackChain: new[] { "Ollama", "OpenRouter" });

        var mockOllamaClient = CreateMockOllamaClient(shouldSucceed: false);
        var mockOpenRouterClient = CreateMockOpenRouterClient(shouldSucceed: true);

        var service = CreateHybridLlmService(aiSettings, mockOllamaClient, mockOpenRouterClient);
        var user = CreateUser(Role.User);

        // Act - Simulate multiple failures to trigger circuit breaker
        for (int i = 0; i < CircuitBreakerFailureThreshold; i++)
        {
            await service.GenerateCompletionAsync(
                "You are a helpful assistant.",
                "Say hello",
                user,
                cancellationToken: TestContext.Current.CancellationToken);
        }

        // Now Ollama circuit should be open, verify fallback to OpenRouter
        var result = await service.GenerateCompletionAsync(
            "You are a helpful assistant.",
            "Say hello after fallback",
            user,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        (result.Success).Should().BeTrue($"Expected success but got failure: {result.ErrorMessage}");
        result.Response.Should().NotBeEmpty();

        // Verify OpenRouter was called for fallback
        mockOpenRouterClient.Verify(
            c => c.GenerateCompletionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<double>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }
    [Fact]
    public async Task BothProvidersDown_ReturnsError()
    {
        // Arrange
        var aiSettings = CreateAiSettings(
            ollamaEnabled: true,
            openRouterEnabled: true,
            preferredProvider: "");

        var mockOllamaClient = CreateMockOllamaClient(shouldSucceed: false);
        var mockOpenRouterClient = CreateMockOpenRouterClient(shouldSucceed: false);

        var service = CreateHybridLlmService(aiSettings, mockOllamaClient, mockOpenRouterClient);
        var user = CreateUser(Role.User);

        // Act - Simulate multiple failures to trigger both circuit breakers
        for (int i = 0; i < CircuitBreakerFailureThreshold; i++)
        {
            await service.GenerateCompletionAsync(
                "You are a helpful assistant.",
                $"Attempt {i}",
                user,
                cancellationToken: TestContext.Current.CancellationToken);
        }

        // Now both circuits should be open
        var result = await service.GenerateCompletionAsync(
            "You are a helpful assistant.",
            "Final attempt",
            user,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        (result.Success).Should().BeFalse("Expected failure when all providers are down");
        result.ErrorMessage.Should().NotBeNull();
        result.ErrorMessage.Should().ContainEquivalentOf("error");
    }
    [Fact]
    public async Task FeatureFlag_DisableOllama_FallsBackToOpenRouter()
    {
        // Arrange - Ollama disabled via feature flag
        var aiSettings = CreateAiSettings(
            ollamaEnabled: false,
            openRouterEnabled: true,
            preferredProvider: "");

        var mockOllamaClient = CreateMockOllamaClient(shouldSucceed: true);
        var mockOpenRouterClient = CreateMockOpenRouterClient(shouldSucceed: true);

        var service = CreateHybridLlmService(aiSettings, mockOllamaClient, mockOpenRouterClient);
        var user = CreateUser(Role.User);

        // Act
        var result = await service.GenerateCompletionAsync(
            "You are a helpful assistant.",
            "Say hello",
            user,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        (result.Success).Should().BeTrue($"Expected success but got failure: {result.ErrorMessage}");
        result.Cost.Provider.Should().Be("OpenRouter");

        // Verify Ollama was NOT called (disabled)
        mockOllamaClient.Verify(
            c => c.GenerateCompletionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<double>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        // Verify OpenRouter was called
        mockOpenRouterClient.Verify(
            c => c.GenerateCompletionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<double>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task FeatureFlag_PreferredProvider_OverridesRouting()
    {
        // Arrange - PreferredProvider set to OpenRouter
        var aiSettings = CreateAiSettings(
            ollamaEnabled: true,
            openRouterEnabled: true,
            preferredProvider: "OpenRouter");

        var mockOllamaClient = CreateMockOllamaClient(shouldSucceed: true);
        var mockOpenRouterClient = CreateMockOpenRouterClient(shouldSucceed: true);

        var service = CreateHybridLlmService(aiSettings, mockOllamaClient, mockOpenRouterClient);
        var user = CreateUser(Role.User);

        // Act
        var result = await service.GenerateCompletionAsync(
            "You are a helpful assistant.",
            "Say hello",
            user,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        (result.Success).Should().BeTrue($"Expected success but got failure: {result.ErrorMessage}");
        result.Cost.Provider.Should().Be("OpenRouter");

        // Verify OpenRouter was called (preferred provider)
        mockOpenRouterClient.Verify(
            c => c.GenerateCompletionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<double>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
    [Fact]
    public async Task CostTracking_OllamaIsFree()
    {
        // Arrange
        var aiSettings = CreateAiSettings(
            ollamaEnabled: true,
            openRouterEnabled: false,
            preferredProvider: "Ollama");

        var mockOllamaClient = CreateMockOllamaClient(shouldSucceed: true);
        var mockOpenRouterClient = CreateMockOpenRouterClient(shouldSucceed: true);

        var service = CreateHybridLlmService(aiSettings, mockOllamaClient, mockOpenRouterClient);
        var user = CreateUser(Role.User);

        // Act
        var result = await service.GenerateCompletionAsync(
            "You are a helpful assistant.",
            "Say hello",
            user,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.Success.Should().BeTrue();
        result.Cost.Provider.Should().Be("Ollama");
        result.Cost.InputCost.Should().Be(0m);
        result.Cost.OutputCost.Should().Be(0m);
        result.Cost.TotalCost.Should().Be(0m);

        // Verify cost was logged via ILlmCostService
        _mockCostService.Verify(
            s => s.LogSuccessAsync(
                It.Is<LlmCompletionResult>(r => r.Cost.TotalCost == 0m),
                It.Is<User?>(u => u != null && u.Id == user.Id),
                It.IsAny<long>(),
                It.IsAny<RequestSource>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CostTracking_OpenRouterHasCost()
    {
        // Arrange
        var aiSettings = CreateAiSettings(
            ollamaEnabled: false,
            openRouterEnabled: true,
            preferredProvider: "OpenRouter");

        var mockOllamaClient = CreateMockOllamaClient(shouldSucceed: true);
        var mockOpenRouterClient = CreateMockOpenRouterClient(shouldSucceed: true);

        var service = CreateHybridLlmService(aiSettings, mockOllamaClient, mockOpenRouterClient);
        var user = CreateUser(Role.User);

        // Act
        var result = await service.GenerateCompletionAsync(
            "You are a helpful assistant.",
            "Say hello",
            user,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.Success.Should().BeTrue();
        result.Cost.Provider.Should().Be("OpenRouter");
        (result.Cost.TotalCost > 0m).Should().BeTrue("OpenRouter should have non-zero cost");

        // Verify cost was logged via ILlmCostService with non-zero cost
        _mockCostService.Verify(
            s => s.LogSuccessAsync(
                It.Is<LlmCompletionResult>(r => r.Cost.TotalCost > 0m),
                It.Is<User?>(u => u != null && u.Id == user.Id),
                It.IsAny<long>(),
                It.IsAny<RequestSource>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
    [Fact]
    public async Task LatencyTracking_RecordsLatency()
    {
        // Arrange
        var aiSettings = CreateAiSettings(
            ollamaEnabled: true,
            openRouterEnabled: true,
            preferredProvider: "Ollama");

        var mockOllamaClient = CreateMockOllamaClient(shouldSucceed: true, latencyMs: MinimumLatencyMs);
        var mockOpenRouterClient = CreateMockOpenRouterClient(shouldSucceed: true);

        var service = CreateHybridLlmService(aiSettings, mockOllamaClient, mockOpenRouterClient);
        var user = CreateUser(Role.User);

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await service.GenerateCompletionAsync(
            "You are a helpful assistant.",
            "Say hello",
            user,
            cancellationToken: TestContext.Current.CancellationToken);
        stopwatch.Stop();

        // Assert
        result.Success.Should().BeTrue();
        (result.Metadata.ContainsKey("latency_ms")).Should().BeTrue();

        var latency = long.Parse(result.Metadata["latency_ms"]);
        (latency > 0).Should().BeTrue("Latency should be tracked");

        // Verify cost was logged via ILlmCostService with latency
        _mockCostService.Verify(
            s => s.LogSuccessAsync(
                It.IsAny<LlmCompletionResult>(),
                It.IsAny<User?>(),
                It.Is<long>(ms => ms > 0),
                It.IsAny<RequestSource>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LatencyComparison_MultipleProviders()
    {
        // Arrange
        var aiSettings = CreateAiSettings(
            ollamaEnabled: true,
            openRouterEnabled: true,
            preferredProvider: "");

        var mockOllamaClient = CreateMockOllamaClient(shouldSucceed: true, latencyMs: 50);
        var mockOpenRouterClient = CreateMockOpenRouterClient(shouldSucceed: true, latencyMs: 150);

        var service = CreateHybridLlmService(aiSettings, mockOllamaClient, mockOpenRouterClient);
        var user = CreateUser(Role.User);

        // Act - Make multiple requests to collect latency data
        var latencies = new List<long>();
        for (int i = 0; i < LatencyCollectionSampleSize; i++)
        {
            var result = await service.GenerateCompletionAsync(
                "You are a helpful assistant.",
                $"Request {i}",
                user,
                cancellationToken: TestContext.Current.CancellationToken);

            if (result.Success && result.Metadata.ContainsKey("latency_ms"))
            {
                latencies.Add(long.Parse(result.Metadata["latency_ms"]));
            }
        }

        // Assert
        latencies.Should().NotBeEmpty();
        latencies.Should().OnlyContain(latency => latency > 0, "All latencies should be positive");

        var avgLatency = latencies.Average();
        (avgLatency > 0).Should().BeTrue("Average latency should be positive");
    }
    /// <summary>
    /// Creates a configured HybridLlmService instance for testing.
    /// Issue #3435: Updated to use strategy-based routing.
    /// </summary>
    private HybridLlmService CreateHybridLlmService(
        IOptions<AiProviderSettings> aiSettings,
        Mock<ILlmClient> mockOllamaClient,
        Mock<ILlmClient> mockOpenRouterClient)
    {
        var clients = new List<ILlmClient>
        {
            mockOllamaClient.Object,
            mockOpenRouterClient.Object
        };

        // Issue #3435: Create mocks for strategy-based routing dependencies
        // Issue #3531: Use Ollama/OpenRouter providers to match test client setup
        // (DefaultStrategyModelMappings now uses DeepSeek/Anthropic which aren't available in tests)
        var mockStrategyMappingService = new Mock<IStrategyModelMappingService>();
        mockStrategyMappingService
            .Setup(s => s.GetModelForStrategyAsync(It.IsAny<RagStrategy>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(("Ollama", "llama3:8b"));
        mockStrategyMappingService
            .Setup(s => s.GetFallbackModelsAsync(It.IsAny<RagStrategy>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "openai/gpt-4o-mini" });

        // Setup IServiceScopeFactory to provide ITierStrategyAccessService
        var mockTierAccessService = new Mock<ITierStrategyAccessService>();
        mockTierAccessService
            .Setup(s => s.HasAccessToStrategyAsync(It.IsAny<LlmUserTier>(), It.IsAny<RagStrategy>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var mockModelConfigRepository = new Mock<IAiModelConfigurationRepository>();

        // Setup IServiceScopeFactory to provide services for both routing strategy and cost logging
        var mockServiceProvider = new Mock<IServiceProvider>();
        mockServiceProvider
            .Setup(sp => sp.GetService(typeof(ITierStrategyAccessService)))
            .Returns(mockTierAccessService.Object);
        mockServiceProvider
            .Setup(sp => sp.GetService(typeof(ILlmCostLogRepository)))
            .Returns(_mockCostLogRepository.Object);
        mockServiceProvider
            .Setup(sp => sp.GetService(typeof(IAiModelConfigurationRepository)))
            .Returns(mockModelConfigRepository.Object);
        var mockScope = new Mock<IServiceScope>();
        mockScope.Setup(s => s.ServiceProvider).Returns(mockServiceProvider.Object);
        var mockScopeFactory = new Mock<IServiceScopeFactory>();
        mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);

        var routingStrategy = new HybridAdaptiveRoutingStrategy(
            mockStrategyMappingService.Object,
            mockScopeFactory.Object,
            aiSettings,
            _strategyLogger);

        // Issue #5487: Create ICircuitBreakerRegistry mock and real LlmProviderSelector
        var circuitBreakerRegistryMock = new Mock<ICircuitBreakerRegistry>();
        circuitBreakerRegistryMock.Setup(r => r.AllowsRequests(It.IsAny<string>())).Returns(true);
        circuitBreakerRegistryMock.Setup(r => r.GetState(It.IsAny<string>())).Returns(CircuitState.Closed);
        circuitBreakerRegistryMock.Setup(r => r.GetMonitoringStatus())
            .Returns(new Dictionary<string, (string, string)>
            {
                ["Ollama"] = ("Closed", "avg: 50ms, p99: 100ms"),
                ["OpenRouter"] = ("Closed", "avg: 150ms, p99: 300ms")
            });
        circuitBreakerRegistryMock.Setup(r => r.GetLatencyStats(It.IsAny<string>()))
            .Returns("avg: 100ms, p99: 200ms");

        var selectorLogger = _loggerFactory.CreateLogger<LlmProviderSelector>();
        var selector = new LlmProviderSelector(
            clients,
            routingStrategy,
            circuitBreakerRegistryMock.Object,
            aiSettings,
            mockModelConfigRepository.Object,
            selectorLogger);

        return new HybridLlmService(
            clients,
            selector,
            circuitBreakerRegistryMock.Object,
            _mockCostService.Object,
            _serviceLogger);
    }

    private static IOptions<AiProviderSettings> CreateAiSettings(
        bool ollamaEnabled,
        bool openRouterEnabled,
        string preferredProvider,
        string[]? fallbackChain = null)
    {
        return Options.Create(new AiProviderSettings
        {
            PreferredProvider = preferredProvider,
            Providers = new Dictionary<string, ProviderConfig>
            {
                ["Ollama"] = new()
                {
                    Enabled = ollamaEnabled,
                    BaseUrl = "http://meepleai-ollama:11434",
                    Models = ["llama3:8b"]
                },
                ["OpenRouter"] = new()
                {
                    Enabled = openRouterEnabled,
                    BaseUrl = "https://openrouter.ai/api/v1",
                    Models = ["openai/gpt-4o-mini"]
                }
            },
            FallbackChain = fallbackChain?.ToList() ?? new List<string> { "Ollama", "OpenRouter" }
        });
    }

    private Mock<ILlmClient> CreateMockOllamaClient(bool shouldSucceed, int latencyMs = 0)
    {
        var mock = new Mock<ILlmClient>();
        mock.Setup(c => c.ProviderName).Returns("Ollama");
        mock.Setup(c => c.SupportsModel(It.IsAny<string>())).Returns((string modelId) => !modelId.Contains('/'));

        mock.Setup(c => c.GenerateCompletionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<double>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (string model, string sys, string usr, double temp, int max, CancellationToken ct) =>
            {
                if (latencyMs > 0)
                    await Task.Delay(latencyMs, ct);

                if (!shouldSucceed)
                    return LlmCompletionResult.CreateFailure("Ollama service unavailable");

                return LlmCompletionResult.CreateSuccess(
                    "Hello from Ollama!",
                    new LlmUsage(10, 5, 15),
                    new LlmCost { InputCost = 0m, OutputCost = 0m, ModelId = model, Provider = "Ollama" },
                    new Dictionary<string, string> { ["provider"] = "Ollama" });
            });

        return mock;
    }

    private Mock<ILlmClient> CreateMockOpenRouterClient(bool shouldSucceed, int latencyMs = 0)
    {
        var mock = new Mock<ILlmClient>();
        mock.Setup(c => c.ProviderName).Returns("OpenRouter");
        mock.Setup(c => c.SupportsModel(It.IsAny<string>())).Returns((string modelId) => modelId.Contains('/'));

        mock.Setup(c => c.GenerateCompletionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<double>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (string model, string sys, string usr, double temp, int max, CancellationToken ct) =>
            {
                if (latencyMs > 0)
                    await Task.Delay(latencyMs, ct);

                if (!shouldSucceed)
                    return LlmCompletionResult.CreateFailure("OpenRouter service unavailable");

                return LlmCompletionResult.CreateSuccess(
                    "Hello from OpenRouter!",
                    new LlmUsage(10, 5, 15),
                    new LlmCost { InputCost = 0.000001m, OutputCost = 0.000003m, ModelId = model, Provider = "OpenRouter" },
                    new Dictionary<string, string> { ["provider"] = "OpenRouter" });
            });

        return mock;
    }

    private static User CreateUser(Role role)
    {
        var email = Email.Parse($"test.{role.Value}@meepleai.dev");
        var password = PasswordHash.Create("TestPass123!");

        return new User(
            Guid.NewGuid(),
            email,
            $"Test {role.Value}",
            password,
            role);
    }
}
