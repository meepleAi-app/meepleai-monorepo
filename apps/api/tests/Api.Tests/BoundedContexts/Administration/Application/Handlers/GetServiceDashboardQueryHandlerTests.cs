using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Commands.Operations;
using Api.BoundedContexts.Administration.Application.Queries.Operations;
using Api.BoundedContexts.Administration.Domain.Models;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Tests for GetServiceDashboardQueryHandler.
/// Issue #132: Enhanced ServiceHealthMatrix backend.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetServiceDashboardQueryHandlerTests
{
    private readonly Mock<IInfrastructureDetailsService> _mockDetailsService;
    private readonly GetServiceDashboardQueryHandler _handler;

    public GetServiceDashboardQueryHandlerTests()
    {
        _mockDetailsService = new Mock<IInfrastructureDetailsService>();
        var mockLogger = new Mock<ILogger<GetServiceDashboardQueryHandler>>();
        _handler = new GetServiceDashboardQueryHandler(
            _mockDetailsService.Object,
            mockLogger.Object
        );
    }

    [Fact]
    public async Task Handle_ReturnsEnhancedDashboard_WithCorrectOverallStatus()
    {
        // Arrange
        var details = CreateMockInfrastructureDetails();
        _mockDetailsService
            .Setup(s => s.GetDetailsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        // Act
        var result = await _handler.Handle(new GetServiceDashboardQuery(), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Overall.State.Should().Be("Healthy");
        result.Overall.TotalServices.Should().Be(3);
        result.Overall.HealthyServices.Should().Be(2);
        result.Overall.DegradedServices.Should().Be(1);
        result.Overall.UnhealthyServices.Should().Be(0);
    }

    [Fact]
    public async Task Handle_MapsServiceDisplayNames_Correctly()
    {
        // Arrange
        var details = CreateMockInfrastructureDetails();
        _mockDetailsService
            .Setup(s => s.GetDetailsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        // Act
        var result = await _handler.Handle(new GetServiceDashboardQuery(), CancellationToken.None);

        // Assert
        var serviceNames = result.Services.Select(s => s.ServiceName).ToList();
        serviceNames.Should().Contain("PostgreSQL");
        serviceNames.Should().Contain("Redis");
        serviceNames.Should().Contain("Embedding Service");
    }

    [Fact]
    public async Task Handle_MapsServiceCategories_Correctly()
    {
        // Arrange
        var details = CreateMockInfrastructureDetails();
        _mockDetailsService
            .Setup(s => s.GetDetailsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        // Act
        var result = await _handler.Handle(new GetServiceDashboardQuery(), CancellationToken.None);

        // Assert
        var postgres = result.Services.First(s => s.ServiceName == "PostgreSQL");
        postgres.Category.Should().Be("Core Infrastructure");

        var embedding = result.Services.First(s => s.ServiceName == "Embedding Service");
        embedding.Category.Should().Be("AI Services");
    }

    [Fact]
    public async Task Handle_EstimatesUptime_BasedOnHealthState()
    {
        // Arrange
        var details = CreateMockInfrastructureDetails();
        _mockDetailsService
            .Setup(s => s.GetDetailsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        // Act
        var result = await _handler.Handle(new GetServiceDashboardQuery(), CancellationToken.None);

        // Assert
        var postgres = result.Services.First(s => s.ServiceName == "PostgreSQL");
        postgres.UptimePercent24h.Should().Be(99.9); // Healthy

        var embedding = result.Services.First(s => s.ServiceName == "Embedding Service");
        embedding.UptimePercent24h.Should().Be(97.0); // Degraded
    }

    [Fact]
    public async Task Handle_SetsLastIncidentAt_ForUnhealthyServices()
    {
        // Arrange
        var details = CreateMockInfrastructureDetails();
        _mockDetailsService
            .Setup(s => s.GetDetailsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        // Act
        var result = await _handler.Handle(new GetServiceDashboardQuery(), CancellationToken.None);

        // Assert
        var postgres = result.Services.First(s => s.ServiceName == "PostgreSQL");
        postgres.LastIncidentAt.Should().BeNull(); // Healthy → no incident

        var embedding = result.Services.First(s => s.ServiceName == "Embedding Service");
        embedding.LastIncidentAt.Should().NotBeNull(); // Degraded → has incident
    }

    [Fact]
    public async Task Handle_IncludesPrometheusMetrics()
    {
        // Arrange
        var details = CreateMockInfrastructureDetails();
        _mockDetailsService
            .Setup(s => s.GetDetailsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        // Act
        var result = await _handler.Handle(new GetServiceDashboardQuery(), CancellationToken.None);

        // Assert
        result.PrometheusMetrics.ApiRequestsLast24h.Should().Be(15000);
        result.PrometheusMetrics.AvgLatencyMs.Should().Be(45.0);
        result.PrometheusMetrics.ErrorRate.Should().Be(0.002);
        result.PrometheusMetrics.LlmCostLast24h.Should().Be(12.5);
    }

    [Fact]
    public async Task Handle_MapsResponseTimeMs_FromTimeSpan()
    {
        // Arrange
        var details = CreateMockInfrastructureDetails();
        _mockDetailsService
            .Setup(s => s.GetDetailsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        // Act
        var result = await _handler.Handle(new GetServiceDashboardQuery(), CancellationToken.None);

        // Assert
        var postgres = result.Services.First(s => s.ServiceName == "PostgreSQL");
        postgres.ResponseTimeMs.Should().Be(15.0);
    }

    [Fact]
    public async Task Handle_DefaultsTrendToStable()
    {
        // Arrange
        var details = CreateMockInfrastructureDetails();
        _mockDetailsService
            .Setup(s => s.GetDetailsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        // Act
        var result = await _handler.Handle(new GetServiceDashboardQuery(), CancellationToken.None);

        // Assert
        foreach (var service in result.Services)
        {
            service.ResponseTimeTrend.Should().Be("stable");
            service.PreviousResponseTimeMs.Should().BeNull();
        }
    }

    [Fact]
    public async Task Handle_ThrowsArgumentNullException_ForNullRequest()
    {
        var act = () => _handler.Handle(null!, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Handle_PropagatesException_FromDetailsService()
    {
        // Arrange
        _mockDetailsService
            .Setup(s => s.GetDetailsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Service unavailable"));

        // Act & Assert
        var act = () => _handler.Handle(new GetServiceDashboardQuery(), CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Handle_UnknownService_DefaultsToExternalApis()
    {
        // Arrange
        var checkedAt = DateTime.UtcNow;
        var services = new List<ServiceHealthStatus>
        {
            new("unknown-service", HealthState.Healthy, null, checkedAt, TimeSpan.FromMilliseconds(10))
        };
        var overall = new OverallHealthStatus(HealthState.Healthy, 1, 1, 0, 0, checkedAt);
        var metrics = new PrometheusMetricsSummary(0, 0, 0, 0);
        var details = new InfrastructureDetails(overall, services, metrics);

        _mockDetailsService
            .Setup(s => s.GetDetailsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        // Act
        var result = await _handler.Handle(new GetServiceDashboardQuery(), CancellationToken.None);

        // Assert
        var service = result.Services.First();
        service.Category.Should().Be("External APIs");
        service.ServiceName.Should().Be("unknown-service"); // No display name mapping
    }

    private static InfrastructureDetails CreateMockInfrastructureDetails()
    {
        var checkedAt = DateTime.UtcNow;
        var services = new List<ServiceHealthStatus>
        {
            new("postgres", HealthState.Healthy, null, checkedAt, TimeSpan.FromMilliseconds(15)),
            new("redis", HealthState.Healthy, null, checkedAt, TimeSpan.FromMilliseconds(2)),
            new("embedding", HealthState.Degraded, "High latency", checkedAt, TimeSpan.FromMilliseconds(3500)),
        };

        var overall = new OverallHealthStatus(HealthState.Healthy, 3, 2, 1, 0, checkedAt);
        var metrics = new PrometheusMetricsSummary(15000, 45.0, 0.002, 12.5);

        return new InfrastructureDetails(overall, services, metrics);
    }
}
