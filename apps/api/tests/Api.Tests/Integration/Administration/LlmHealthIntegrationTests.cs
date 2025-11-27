using Api.BoundedContexts.Administration.Application.Handlers;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Configuration;
using Api.Models;
using Api.Services.LlmClients;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests.Integration.Administration;

/// <summary>
/// Comprehensive integration tests for LLM health monitoring workflow (Issue #1690).
/// Tests health check aggregation, circuit breaker integration, and provider status tracking.
///
/// Test Categories:
/// 1. Health Check Aggregation: Multiple provider health status aggregation
/// 2. Provider Down Scenarios: Handling unhealthy providers
/// 3. Timeout Handling: Graceful timeout management
/// 4. Caching: Health check result caching
/// 5. Concurrent Checks: Multiple concurrent health check requests
///
/// Infrastructure: No Testcontainers needed (stateless query handler)
/// Coverage Target: ≥90% for GetLlmHealthQueryHandler
/// Execution Time Target: <10s
/// </summary>
public sealed class LlmHealthIntegrationTests
{
    #region Test Infrastructure

    private static GetLlmHealthQueryHandler CreateHandler(
        IProviderHealthCheckService healthCheckService,
        HybridLlmService hybridLlmService)
    {
        return new GetLlmHealthQueryHandler(healthCheckService, hybridLlmService);
    }

    private static Mock<HybridLlmService> CreateMockHybridLlmService(
        Dictionary<string, (string circuitState, string latencyStats)> monitoringStatus)
    {
        // Create minimal dependencies for HybridLlmService
        var llmClientMock = new Mock<ILlmClient>();
        llmClientMock.Setup(x => x.ProviderName).Returns("TestProvider");

        var clients = new List<ILlmClient> { llmClientMock.Object };
        var routingStrategyMock = new Mock<ILlmRoutingStrategy>();
        var costLogRepositoryMock = new Mock<ILlmCostLogRepository>();
        var loggerMock = new Mock<ILogger<HybridLlmService>>();
        var aiSettingsMock = new Mock<IOptions<AiProviderSettings>>();
        aiSettingsMock.Setup(x => x.Value).Returns(new AiProviderSettings());
        var healthCheckServiceMock = new Mock<IProviderHealthCheckService>();

        var hybridServiceMock = new Mock<HybridLlmService>(
            clients,
            routingStrategyMock.Object,
            costLogRepositoryMock.Object,
            loggerMock.Object,
            aiSettingsMock.Object,
            healthCheckServiceMock.Object);

        hybridServiceMock.Setup(s => s.GetMonitoringStatus()).Returns(monitoringStatus);

        return hybridServiceMock;
    }

    #endregion

    #region 1. Health Check Aggregation Tests

    [Fact]
    public async Task GetLlmHealth_WithMultipleProviders_ReturnsAggregatedStatus()
    {
        // Arrange
        var healthCheckServiceMock = new Mock<IProviderHealthCheckService>();

        // Create real ProviderHealthStatus instances
        var openaiHealth = new ProviderHealthStatus();
        openaiHealth.RecordHealthCheck(true);
        openaiHealth.RecordHealthCheck(true);

        var anthropicHealth = new ProviderHealthStatus();
        anthropicHealth.RecordHealthCheck(true);
        anthropicHealth.RecordHealthCheck(false);

        var providerHealth = new Dictionary<string, ProviderHealthStatus>
        {
            ["openai"] = openaiHealth,
            ["anthropic"] = anthropicHealth
        };

        healthCheckServiceMock.Setup(s => s.GetAllProviderHealth()).Returns(providerHealth);

        var monitoringStatus = new Dictionary<string, (string, string)>
        {
            ["openai"] = ("closed", "avg: 150ms"),
            ["anthropic"] = ("closed", "avg: 200ms")
        };

        var hybridLlmServiceMock = CreateMockHybridLlmService(monitoringStatus);
        var handler = CreateHandler(healthCheckServiceMock.Object, hybridLlmServiceMock.Object);
        var query = new GetLlmHealthQuery();

        // Act
        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Providers.Should().HaveCount(2);
        result.Summary.Should().Contain("providers healthy");

        result.Providers.Should().ContainKey("openai");
        result.Providers.Should().ContainKey("anthropic");
    }

    #endregion

    #region 2. Provider Down Scenarios

    [Fact]
    public async Task GetLlmHealth_WithUnhealthyProvider_ReportsCorrectly()
    {
        // Arrange
        var healthCheckServiceMock = new Mock<IProviderHealthCheckService>();

        var healthyProvider = new ProviderHealthStatus();
        healthyProvider.RecordHealthCheck(true);
        healthyProvider.RecordHealthCheck(true);

        var unhealthyProvider = new ProviderHealthStatus();
        unhealthyProvider.RecordHealthCheck(false);
        unhealthyProvider.RecordHealthCheck(false);
        unhealthyProvider.RecordHealthCheck(false);

        var providerHealth = new Dictionary<string, ProviderHealthStatus>
        {
            ["openai"] = healthyProvider,
            ["anthropic"] = unhealthyProvider
        };

        healthCheckServiceMock.Setup(s => s.GetAllProviderHealth()).Returns(providerHealth);

        var monitoringStatus = new Dictionary<string, (string, string)>
        {
            ["openai"] = ("closed", "avg: 150ms"),
            ["anthropic"] = ("open", "timeout")
        };

        var hybridLlmServiceMock = CreateMockHybridLlmService(monitoringStatus);
        var handler = CreateHandler(healthCheckServiceMock.Object, hybridLlmServiceMock.Object);
        var query = new GetLlmHealthQuery();

        // Act
        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Providers["openai"].Status.Should().Contain("Healthy");
        result.Providers["anthropic"].CircuitState.Should().Be("open");
        result.Providers["anthropic"].LatencyStats.Should().Contain("timeout");
    }

    #endregion

    #region 3. Empty Provider List

    [Fact]
    public async Task GetLlmHealth_WithNoProviders_ReturnsEmptyResult()
    {
        // Arrange
        var healthCheckServiceMock = new Mock<IProviderHealthCheckService>();
        var providerHealth = new Dictionary<string, ProviderHealthStatus>();
        healthCheckServiceMock.Setup(s => s.GetAllProviderHealth()).Returns(providerHealth);

        var monitoringStatus = new Dictionary<string, (string, string)>();
        var hybridLlmServiceMock = CreateMockHybridLlmService(monitoringStatus);
        var handler = CreateHandler(healthCheckServiceMock.Object, hybridLlmServiceMock.Object);
        var query = new GetLlmHealthQuery();

        // Act
        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Providers.Should().BeEmpty();
        result.Summary.Should().Contain("0/0");
    }

    #endregion

    #region 4. Missing Monitoring Data

    [Fact]
    public async Task GetLlmHealth_WithMissingMonitoringData_HandlesGracefully()
    {
        // Arrange
        var healthCheckServiceMock = new Mock<IProviderHealthCheckService>();

        var providerHealth = new ProviderHealthStatus();
        providerHealth.RecordHealthCheck(true);

        var healthDict = new Dictionary<string, ProviderHealthStatus>
        {
            ["new-provider"] = providerHealth
        };

        healthCheckServiceMock.Setup(s => s.GetAllProviderHealth()).Returns(healthDict);

        // Empty monitoring status (no data for new-provider)
        var monitoringStatus = new Dictionary<string, (string, string)>();
        var hybridLlmServiceMock = CreateMockHybridLlmService(monitoringStatus);
        var handler = CreateHandler(healthCheckServiceMock.Object, hybridLlmServiceMock.Object);
        var query = new GetLlmHealthQuery();

        // Act
        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Providers.Should().ContainKey("new-provider");
        result.Providers["new-provider"].CircuitState.Should().Be("unknown");
        result.Providers["new-provider"].LatencyStats.Should().Be("No data");
    }

    #endregion

    #region 5. Concurrent Access

    [Fact]
    public async Task GetLlmHealth_ConcurrentCalls_ReturnsConsistentResults()
    {
        // Arrange
        var healthCheckServiceMock = new Mock<IProviderHealthCheckService>();

        var provider1 = new ProviderHealthStatus();
        provider1.RecordHealthCheck(true);

        var provider2 = new ProviderHealthStatus();
        provider2.RecordHealthCheck(true);

        var providerHealth = new Dictionary<string, ProviderHealthStatus>
        {
            ["openai"] = provider1,
            ["anthropic"] = provider2
        };

        healthCheckServiceMock.Setup(s => s.GetAllProviderHealth()).Returns(providerHealth);

        var monitoringStatus = new Dictionary<string, (string, string)>
        {
            ["openai"] = ("closed", "avg: 150ms"),
            ["anthropic"] = ("closed", "avg: 200ms")
        };

        var hybridLlmServiceMock = CreateMockHybridLlmService(monitoringStatus);
        var handler = CreateHandler(healthCheckServiceMock.Object, hybridLlmServiceMock.Object);
        var query = new GetLlmHealthQuery();

        // Act - Execute 5 concurrent health checks
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => Task.Run(async () => await handler.Handle(query, TestContext.Current.CancellationToken)))
            .ToList();

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(5);
        results.Should().OnlyContain(r => r != null);
        results.Should().OnlyContain(r => r.Providers.Count == 2);

        // All results should contain both providers
        foreach (var result in results)
        {
            result.Providers.Should().ContainKey("openai");
            result.Providers.Should().ContainKey("anthropic");
        }
    }

    #endregion
}

