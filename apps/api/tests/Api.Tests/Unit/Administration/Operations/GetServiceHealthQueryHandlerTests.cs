using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Application.Queries.Operations;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using Xunit;

namespace Api.Tests.Unit.Administration.Operations;

/// <summary>
/// Unit tests for GetServiceHealthQueryHandler.
/// Issue #3696: Operations - Service Control Panel.
/// </summary>
public sealed class GetServiceHealthQueryHandlerTests
{
    private readonly Mock<HealthCheckService> _mockHealthCheckService;
    private readonly GetServiceHealthQueryHandler _handler;

    public GetServiceHealthQueryHandlerTests()
    {
        _mockHealthCheckService = new Mock<HealthCheckService>();
        _handler = new GetServiceHealthQueryHandler(_mockHealthCheckService.Object);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("BoundedContext", "Administration")]
    public async Task Handle_AllServicesHealthy_ReturnsHealthyStatus()
    {
        // Arrange
        var query = new GetServiceHealthQuery();
        var healthReport = CreateHealthReport(
            ("PostgreSQL", HealthStatus.Healthy, "critical"),
            ("Redis", HealthStatus.Healthy, "critical"),
            ("Qdrant", HealthStatus.Healthy, "critical")
        );

        _mockHealthCheckService
            .Setup(x => x.CheckHealthAsync(It.IsAny<Func<HealthCheckRegistration, bool>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(healthReport);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal("Healthy", result.OverallStatus);
        Assert.Equal(3, result.Services.Count);
        Assert.All(result.Services, s => Assert.Equal("Healthy", s.Status));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("BoundedContext", "Administration")]
    public async Task Handle_CriticalServiceUnhealthy_ReturnsUnhealthyStatus()
    {
        // Arrange
        var query = new GetServiceHealthQuery();
        var healthReport = CreateHealthReport(
            ("PostgreSQL", HealthStatus.Unhealthy, "critical"),
            ("Redis", HealthStatus.Healthy, "critical")
        );

        _mockHealthCheckService
            .Setup(x => x.CheckHealthAsync(It.IsAny<Func<HealthCheckRegistration, bool>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(healthReport);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal("Unhealthy", result.OverallStatus);
        var postgresService = result.Services.First(s => s.ServiceName == "PostgreSQL");
        Assert.Equal("Unhealthy", postgresService.Status);
        Assert.True(postgresService.IsCritical);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("BoundedContext", "Administration")]
    public async Task Handle_NonCriticalServiceUnhealthy_ReturnsDegradedStatus()
    {
        // Arrange
        var query = new GetServiceHealthQuery();
        var healthReport = CreateHealthReport(
            ("PostgreSQL", HealthStatus.Healthy, "critical"),
            ("BGG Sync", HealthStatus.Unhealthy, "")
        );

        _mockHealthCheckService
            .Setup(x => x.CheckHealthAsync(It.IsAny<Func<HealthCheckRegistration, bool>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(healthReport);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal("Degraded", result.OverallStatus);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("BoundedContext", "Administration")]
    public async Task Handle_ServiceWithDescription_IncludesDescription()
    {
        // Arrange
        var query = new GetServiceHealthQuery();
        var healthReport = CreateHealthReport(
            ("PostgreSQL", HealthStatus.Healthy, "critical", "Database connection successful")
        );

        _mockHealthCheckService
            .Setup(x => x.CheckHealthAsync(It.IsAny<Func<HealthCheckRegistration, bool>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(healthReport);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Single(result.Services);
        Assert.Equal("Database connection successful", result.Services[0].Description);
    }

    private static HealthReport CreateHealthReport(
        params (string Name, HealthStatus Status, string Tags, string? Description)[] entries)
    {
        var healthEntries = entries.ToDictionary(
            e => e.Name,
            e => new HealthReportEntry(
                status: e.Status,
                description: e.Description,
                duration: TimeSpan.Zero,
                exception: null,
                data: null,
                tags: string.IsNullOrWhiteSpace(e.Tags) ? Array.Empty<string>() : e.Tags.Split(',')
            )
        );

        var overallStatus = healthEntries.Values.All(e => e.Status == HealthStatus.Healthy)
            ? HealthStatus.Healthy
            : HealthStatus.Unhealthy;

        return new HealthReport(healthEntries, overallStatus, TimeSpan.Zero);
    }

    private static HealthReport CreateHealthReport(
        params (string Name, HealthStatus Status, string Tags)[] entries)
    {
        return CreateHealthReport(entries.Select(e => (e.Name, e.Status, e.Tags, (string?)null)).ToArray());
    }
}
