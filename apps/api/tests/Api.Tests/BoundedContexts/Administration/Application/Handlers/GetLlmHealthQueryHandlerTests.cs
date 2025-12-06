using Api.BoundedContexts.Administration.Application.Handlers;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Configuration;
using Api.Services.LlmClients;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Tests for GetLlmHealthQueryHandler.
/// Tests LLM provider health monitoring and circuit breaker status aggregation.
/// NOTE: Complex handler with service dependencies - focused on construction and basic scenarios.
/// RESOLVED: Issue #1690 - Integration tests added in LlmHealthIntegrationTests.cs.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetLlmHealthQueryHandlerTests
{
    private readonly Mock<IProviderHealthCheckService> _healthCheckServiceMock;
    private readonly Mock<HybridLlmService> _hybridLlmServiceMock;

    public GetLlmHealthQueryHandlerTests()
    {
        _healthCheckServiceMock = new Mock<IProviderHealthCheckService>();

        // Create mocks for HybridLlmService dependencies
        var llmClientMock = new Mock<ILlmClient>();
        llmClientMock.Setup(x => x.ProviderName).Returns("TestProvider");

        var clients = new List<ILlmClient> { llmClientMock.Object };
        var routingStrategyMock = new Mock<ILlmRoutingStrategy>();
        var costLogRepositoryMock = new Mock<ILlmCostLogRepository>();
        var loggerMock = new Mock<ILogger<HybridLlmService>>();
        var aiSettingsMock = new Mock<IOptions<AiProviderSettings>>();
        var healthCheckServiceMock = new Mock<IProviderHealthCheckService>();

        // Configure aiSettingsMock to return a valid AiProviderSettings
        aiSettingsMock.Setup(x => x.Value).Returns(new AiProviderSettings());

        _hybridLlmServiceMock = new Mock<HybridLlmService>(
            clients,
            routingStrategyMock.Object,
            costLogRepositoryMock.Object,
            loggerMock.Object,
            aiSettingsMock.Object,
            healthCheckServiceMock.Object);
    }
    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Act
        var handler = new GetLlmHealthQueryHandler(
            _healthCheckServiceMock.Object,
            _hybridLlmServiceMock.Object);

        // Assert
        Assert.NotNull(handler);
    }

    [Fact]
    public void Constructor_WithNullHealthCheckService_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new GetLlmHealthQueryHandler(
                null!,
                _hybridLlmServiceMock.Object));
    }

    [Fact]
    public void Constructor_WithNullHybridLlmService_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new GetLlmHealthQueryHandler(
                _healthCheckServiceMock.Object,
                null!));
    }
    [Fact]
    public void Query_ConstructsCorrectly()
    {
        // Act
        var query = new GetLlmHealthQuery();

        // Assert
        Assert.NotNull(query);
    }
    // NOTE: Full workflow tests (health check aggregation, circuit breaker status,
    // latency statistics, success rate calculation, history tracking)
    // should be in integration test suite due to complex service dependencies.
    //
    // Key scenarios for integration tests (ISSUE-962, BGAI-020):
    // 1. Multiple provider health aggregation
    // 2. Circuit breaker state tracking (Open, HalfOpen, Closed)
    // 3. Latency statistics calculation
    // 4. Success rate computation from health check history
    // 5. Provider availability determination
    // 6. Summary generation (healthy/total providers)
    // 7. Empty provider list handling
    // 8. Mixed health statuses (some healthy, some unhealthy)
    // 9. Provider with no monitoring data
    // 10. Real-time health status updates
    //
    // See integration-tests.yml workflow and ProviderHealthCheckServiceTests.cs
    // for health check service unit tests.
}