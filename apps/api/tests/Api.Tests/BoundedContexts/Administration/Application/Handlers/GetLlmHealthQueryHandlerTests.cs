using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Tests.Constants;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Tests for GetLlmHealthQueryHandler.
/// Tests LLM provider health monitoring and circuit breaker status aggregation.
/// NOTE: Complex handler with service dependencies - focused on construction and basic scenarios.
/// RESOLVED: Issue #1690 - Integration tests added in LlmHealthIntegrationTests.cs.
/// Issue #5487: Updated to use ICircuitBreakerRegistry directly instead of HybridLlmService.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetLlmHealthQueryHandlerTests
{
    private readonly Mock<IProviderHealthCheckService> _healthCheckServiceMock;
    private readonly Mock<ICircuitBreakerRegistry> _circuitBreakerRegistryMock;

    public GetLlmHealthQueryHandlerTests()
    {
        _healthCheckServiceMock = new Mock<IProviderHealthCheckService>();
        _circuitBreakerRegistryMock = new Mock<ICircuitBreakerRegistry>();

        _circuitBreakerRegistryMock
            .Setup(r => r.GetMonitoringStatus())
            .Returns(new Dictionary<string, (string circuitState, string latencyStats)>());
    }
    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Act
        var handler = new GetLlmHealthQueryHandler(
            _healthCheckServiceMock.Object,
            _circuitBreakerRegistryMock.Object);

        // Assert
        handler.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullHealthCheckService_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () =>
            new GetLlmHealthQueryHandler(
                null!,
                _circuitBreakerRegistryMock.Object);
act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullCircuitBreakerRegistry_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () =>
            new GetLlmHealthQueryHandler(
                _healthCheckServiceMock.Object,
                null!);
act.Should().Throw<ArgumentNullException>();
    }
    [Fact]
    public void Query_ConstructsCorrectly()
    {
        // Act
        var query = new GetLlmHealthQuery();

        // Assert
        query.Should().NotBeNull();
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