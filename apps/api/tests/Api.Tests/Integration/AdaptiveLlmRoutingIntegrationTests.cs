using System.Diagnostics;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Configuration;
using Api.Services;
using Api.Services.LlmClients;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

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
[Collection("LlmRouting")]
[Trait("Category", "Integration")]
public class AdaptiveLlmRoutingIntegrationTests : IAsyncLifetime
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<HybridLlmService> _serviceLogger;
    private readonly ILogger<HybridAdaptiveRoutingStrategy> _strategyLogger;
    private readonly ILogger<OllamaLlmClient> _ollamaLogger;
    private readonly ILogger<OpenRouterLlmClient> _openRouterLogger;
    private readonly IConfiguration _configuration;
    private readonly Mock<ILlmCostLogRepository> _mockCostLogRepository;

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
        _ollamaLogger = _loggerFactory.CreateLogger<OllamaLlmClient>();
        _openRouterLogger = _loggerFactory.CreateLogger<OpenRouterLlmClient>();

        // Default configuration
        var configData = new Dictionary<string, string>
        {
            ["LlmRouting:AnonymousModel"] = "llama3:8b",
            ["LlmRouting:AnonymousOpenRouterPercent"] = "20",
            ["LlmRouting:UserModel"] = "llama3:8b",
            ["LlmRouting:UserOpenRouterPercent"] = "20",
            ["LlmRouting:EditorModel"] = "llama3:8b",
            ["LlmRouting:EditorOpenRouterPercent"] = "50",
            ["LlmRouting:AdminModel"] = "llama3:8b",
            ["LlmRouting:AdminOpenRouterPercent"] = "80",
            ["LlmRouting:PremiumModel"] = "openai/gpt-4o-mini",
            ["OllamaUrl"] = "http://meepleai-ollama:11434",
            ["OPENROUTER_API_KEY"] = "test-key-not-used"
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData!)
            .Build();

        _mockCostLogRepository = new Mock<ILlmCostLogRepository>();
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
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _loggerFactory?.Dispose();
        return ValueTask.CompletedTask;
    }

    #region Test 1: Ollama Primary Success

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
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Success, $"Expected success but got failure: {result.ErrorMessage}");
        Assert.NotEmpty(result.Response);
        Assert.Equal("Ollama", result.Cost.Provider);
        Assert.Equal(0m, result.Cost.TotalCost); // Ollama is free

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

    #endregion

    #region Test 2: Ollama Fails → OpenRouter Fallback

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
                TestContext.Current.CancellationToken);
        }

        // Now Ollama circuit should be open, verify fallback to OpenRouter
        var result = await service.GenerateCompletionAsync(
            "You are a helpful assistant.",
            "Say hello after fallback",
            user,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Success, $"Expected success but got failure: {result.ErrorMessage}");
        Assert.NotEmpty(result.Response);

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

    #endregion

    #region Test 3: Both Providers Down → Error Handling

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
                TestContext.Current.CancellationToken);
        }

        // Now both circuits should be open
        var result = await service.GenerateCompletionAsync(
            "You are a helpful assistant.",
            "Final attempt",
            user,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Success, "Expected failure when all providers are down");
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("error", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Test 4: Feature Flag Toggle

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
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Success, $"Expected success but got failure: {result.ErrorMessage}");
        Assert.Equal("OpenRouter", result.Cost.Provider);

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
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Success, $"Expected success but got failure: {result.ErrorMessage}");
        Assert.Equal("OpenRouter", result.Cost.Provider);

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

    #endregion

    #region Test 5: Cost Tracking Accuracy

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
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Ollama", result.Cost.Provider);
        Assert.Equal(0m, result.Cost.InputCost);
        Assert.Equal(0m, result.Cost.OutputCost);
        Assert.Equal(0m, result.Cost.TotalCost);

        // Verify cost was logged
        _mockCostLogRepository.Verify(
            r => r.LogCostAsync(
                It.Is<Guid?>(id => id == user.Id),
                It.Is<string>(role => role == "user"),
                It.Is<LlmCostCalculation>(calc => calc.TotalCost == 0m),
                It.IsAny<string>(),
                It.Is<bool>(success => success == true),
                It.IsAny<string?>(),
                It.IsAny<int>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
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
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("OpenRouter", result.Cost.Provider);
        Assert.True(result.Cost.TotalCost > 0m, "OpenRouter should have non-zero cost");

        // Verify cost was logged with non-zero cost
        _mockCostLogRepository.Verify(
            r => r.LogCostAsync(
                It.Is<Guid?>(id => id == user.Id),
                It.Is<string>(role => role == "user"),
                It.Is<LlmCostCalculation>(calc => calc.TotalCost > 0m),
                It.IsAny<string>(),
                It.Is<bool>(success => success == true),
                It.IsAny<string?>(),
                It.IsAny<int>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Test 6: Latency Comparison

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
            TestContext.Current.CancellationToken);
        stopwatch.Stop();

        // Assert
        Assert.True(result.Success);
        Assert.True(result.Metadata.ContainsKey("latency_ms"));

        var latency = long.Parse(result.Metadata["latency_ms"]);
        Assert.True(latency > 0, "Latency should be tracked");

        // Verify latency was logged
        _mockCostLogRepository.Verify(
            r => r.LogCostAsync(
                It.IsAny<Guid?>(),
                It.IsAny<string>(),
                It.IsAny<LlmCostCalculation>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<string?>(),
                It.Is<int>(ms => ms > 0),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
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
                TestContext.Current.CancellationToken);

            if (result.Success && result.Metadata.ContainsKey("latency_ms"))
            {
                latencies.Add(long.Parse(result.Metadata["latency_ms"]));
            }
        }

        // Assert
        Assert.NotEmpty(latencies);
        Assert.All(latencies, latency => Assert.True(latency > 0, "All latencies should be positive"));

        var avgLatency = latencies.Average();
        var monitoringStatus = service.GetMonitoringStatus();

        Assert.NotEmpty(monitoringStatus);
        foreach (var (provider, (circuitState, latencyStats)) in monitoringStatus)
        {
            Assert.NotNull(circuitState);
            Assert.NotNull(latencyStats);
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a configured HybridLlmService instance for testing
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

        var routingStrategy = new HybridAdaptiveRoutingStrategy(
            _strategyLogger,
            _configuration,
            aiSettings);

        return new HybridLlmService(
            clients,
            routingStrategy,
            _mockCostLogRepository.Object,
            _serviceLogger,
            aiSettings,
            healthCheckService: null);
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

    #endregion
}

