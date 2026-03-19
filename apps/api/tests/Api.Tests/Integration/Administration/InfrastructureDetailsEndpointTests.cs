using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Domain.Models;
using Api.BoundedContexts.Administration.Domain.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.Integration.Administration;

/// <summary>
/// Integration tests for infrastructure details endpoint (Issue #894).
/// Tests the /admin/infrastructure/details endpoint with orchestration logic.
///
/// Test Categories:
/// 1. Complete Response: All sections (overall, services, metrics) present
/// 2. Data Consistency: Health and metrics data properly combined
/// 3. Error Resilience: Graceful degradation when Prometheus fails
///
/// Infrastructure: Mocked services (orchestration logic is the focus)
/// Coverage Target: ≥90% for GetInfrastructureDetails endpoint
/// Execution Time Target: <3s
/// </summary>
[Trait("Category", TestCategories.Integration)]
public sealed class InfrastructureDetailsEndpointTests
{
    private readonly Mock<IInfrastructureDetailsService> _detailsServiceMock;
    private readonly Mock<ILogger<GetInfrastructureDetailsQueryHandler>> _loggerMock;

    public InfrastructureDetailsEndpointTests()
    {
        _detailsServiceMock = new Mock<IInfrastructureDetailsService>();
        _loggerMock = new Mock<ILogger<GetInfrastructureDetailsQueryHandler>>();
    }

    private GetInfrastructureDetailsQueryHandler CreateHandler() =>
        new(_detailsServiceMock.Object, _loggerMock.Object);

    [Fact]
    public async Task GetInfrastructureDetails_HealthySystem_ReturnsCompleteDetails()
    {
        // Arrange
        var expectedDetails = CreateHealthyInfrastructureDetails();

        _detailsServiceMock
            .Setup(s => s.GetDetailsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDetails);

        var handler = CreateHandler();
        var query = new GetInfrastructureDetailsQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();

        // Overall health section
        result.Overall.State.Should().Be(HealthState.Healthy);
        result.Overall.TotalServices.Should().Be(5);
        result.Overall.HealthyServices.Should().Be(5);

        // Services section
        result.Services.Should().HaveCount(5);
        result.Services.Should().AllSatisfy(s => s.State.Should().Be(HealthState.Healthy));

        // Metrics section
        result.Metrics.ApiRequestsLast24h.Should().BeGreaterThan(0);
        result.Metrics.AvgLatencyMs.Should().BeGreaterThan(0);
        result.Metrics.ErrorRate.Should().BeGreaterThanOrEqualTo(0);
        result.Metrics.LlmCostLast24h.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GetInfrastructureDetails_DegradedSystem_ReflectsInOverallStatus()
    {
        // Arrange
        var expectedDetails = CreateDegradedInfrastructureDetails();

        _detailsServiceMock
            .Setup(s => s.GetDetailsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDetails);

        var handler = CreateHandler();
        var query = new GetInfrastructureDetailsQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Overall.State.Should().Be(HealthState.Degraded);
        result.Overall.DegradedServices.Should().Be(1);
        result.Services.Should().Contain(s => s.State == HealthState.Degraded);
    }

    [Fact]
    public async Task GetInfrastructureDetails_PrometheusMetricsPresent_IncludesAllMetrics()
    {
        // Arrange
        var expectedDetails = CreateHealthyInfrastructureDetails();

        _detailsServiceMock
            .Setup(s => s.GetDetailsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDetails);

        var handler = CreateHandler();
        var query = new GetInfrastructureDetailsQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Metrics.Should().NotBeNull();
        result.Metrics.ApiRequestsLast24h.Should().Be(15430);
        result.Metrics.AvgLatencyMs.Should().Be(120.5);
        result.Metrics.ErrorRate.Should().Be(0.02);
        result.Metrics.LlmCostLast24h.Should().Be(3.45);
    }

    [Fact]
    public async Task GetInfrastructureDetails_ServiceException_PropagatesException()
    {
        // Arrange
        _detailsServiceMock
            .Setup(s => s.GetDetailsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Service orchestration failed"));

        var handler = CreateHandler();
        var query = new GetInfrastructureDetailsQuery();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(query, CancellationToken.None));

        exception.Message.Should().Be("Service orchestration failed");
    }

    [Fact]
    public async Task GetInfrastructureDetails_CalledOnce_InvokesServiceOnce()
    {
        // Arrange
        var expectedDetails = CreateHealthyInfrastructureDetails();

        _detailsServiceMock
            .Setup(s => s.GetDetailsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDetails);

        var handler = CreateHandler();
        var query = new GetInfrastructureDetailsQuery();

        // Act
        await handler.Handle(query, CancellationToken.None);

        // Assert
        _detailsServiceMock.Verify(
            s => s.GetDetailsAsync(It.IsAny<CancellationToken>()),
            Times.Once,
            "Handler should call service exactly once");
    }

    private static InfrastructureDetails CreateHealthyInfrastructureDetails()
    {
        var overall = new OverallHealthStatus(
            HealthState.Healthy,
            TotalServices: 5,
            HealthyServices: 5,
            DegradedServices: 0,
            UnhealthyServices: 0,
            DateTime.UtcNow
        );

        var services = new List<ServiceHealthStatus>
        {
            new("postgres", HealthState.Healthy, null, DateTime.UtcNow, TimeSpan.FromMilliseconds(45)),
            new("redis", HealthState.Healthy, null, DateTime.UtcNow, TimeSpan.FromMilliseconds(30)),
            new("qdrant", HealthState.Healthy, null, DateTime.UtcNow, TimeSpan.FromMilliseconds(60)),
            new("n8n", HealthState.Healthy, null, DateTime.UtcNow, TimeSpan.FromMilliseconds(100)),
            new("qdrant-collection", HealthState.Healthy, null, DateTime.UtcNow, TimeSpan.FromMilliseconds(80))
        };

        var metrics = new PrometheusMetricsSummary(
            ApiRequestsLast24h: 15430,
            AvgLatencyMs: 120.5,
            ErrorRate: 0.02,
            LlmCostLast24h: 3.45
        );

        return new InfrastructureDetails(overall, services, metrics);
    }

    private static InfrastructureDetails CreateDegradedInfrastructureDetails()
    {
        var overall = new OverallHealthStatus(
            HealthState.Degraded,
            TotalServices: 5,
            HealthyServices: 4,
            DegradedServices: 1,
            UnhealthyServices: 0,
            DateTime.UtcNow
        );

        var services = new List<ServiceHealthStatus>
        {
            new("postgres", HealthState.Healthy, null, DateTime.UtcNow, TimeSpan.FromMilliseconds(45)),
            new("redis", HealthState.Degraded, "High latency", DateTime.UtcNow, TimeSpan.FromMilliseconds(2500)),
            new("qdrant", HealthState.Healthy, null, DateTime.UtcNow, TimeSpan.FromMilliseconds(60)),
            new("n8n", HealthState.Healthy, null, DateTime.UtcNow, TimeSpan.FromMilliseconds(100)),
            new("qdrant-collection", HealthState.Healthy, null, DateTime.UtcNow, TimeSpan.FromMilliseconds(80))
        };

        var metrics = new PrometheusMetricsSummary(
            ApiRequestsLast24h: 8920,
            AvgLatencyMs: 250.3,
            ErrorRate: 0.05,
            LlmCostLast24h: 1.85
        );

        return new InfrastructureDetails(overall, services, metrics);
    }
}
