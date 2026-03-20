using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Commands.Operations;
using Api.BoundedContexts.Administration.Application.Queries.Operations;
using Api.BoundedContexts.Administration.Domain.Models;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

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
        Assert.NotNull(result);
        Assert.Equal("Healthy", result.Overall.State);
        Assert.Equal(3, result.Overall.TotalServices);
        Assert.Equal(2, result.Overall.HealthyServices);
        Assert.Equal(1, result.Overall.DegradedServices);
        Assert.Equal(0, result.Overall.UnhealthyServices);
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
        Assert.Contains("PostgreSQL", serviceNames);
        Assert.Contains("Redis", serviceNames);
        Assert.Contains("Embedding Service", serviceNames);
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
        Assert.Equal("Core Infrastructure", postgres.Category);

        var embedding = result.Services.First(s => s.ServiceName == "Embedding Service");
        Assert.Equal("AI Services", embedding.Category);
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
        Assert.Equal(99.9, postgres.UptimePercent24h); // Healthy

        var embedding = result.Services.First(s => s.ServiceName == "Embedding Service");
        Assert.Equal(97.0, embedding.UptimePercent24h); // Degraded
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
        Assert.Null(postgres.LastIncidentAt); // Healthy → no incident

        var embedding = result.Services.First(s => s.ServiceName == "Embedding Service");
        Assert.NotNull(embedding.LastIncidentAt); // Degraded → has incident
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
        Assert.Equal(15000, result.PrometheusMetrics.ApiRequestsLast24h);
        Assert.Equal(45.0, result.PrometheusMetrics.AvgLatencyMs);
        Assert.Equal(0.002, result.PrometheusMetrics.ErrorRate);
        Assert.Equal(12.5, result.PrometheusMetrics.LlmCostLast24h);
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
        Assert.Equal(15.0, postgres.ResponseTimeMs);
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
            Assert.Equal("stable", service.ResponseTimeTrend);
            Assert.Null(service.PreviousResponseTimeMs);
        }
    }

    [Fact]
    public async Task Handle_ThrowsArgumentNullException_ForNullRequest()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _handler.Handle(null!, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_PropagatesException_FromDetailsService()
    {
        // Arrange
        _mockDetailsService
            .Setup(s => s.GetDetailsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Service unavailable"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(new GetServiceDashboardQuery(), CancellationToken.None));
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
        Assert.Equal("External APIs", service.Category);
        Assert.Equal("unknown-service", service.ServiceName); // No display name mapping
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
