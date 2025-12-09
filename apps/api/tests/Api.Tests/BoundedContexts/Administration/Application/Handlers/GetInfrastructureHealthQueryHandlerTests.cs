using Api.BoundedContexts.Administration.Application.Handlers;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Tests for GetInfrastructureHealthQueryHandler.
/// Issue #891: Infrastructure health monitoring service tests.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetInfrastructureHealthQueryHandlerTests
{
    private readonly Mock<IInfrastructureHealthService> _mockHealthService;
    private readonly Mock<ILogger<GetInfrastructureHealthQueryHandler>> _mockLogger;
    private readonly GetInfrastructureHealthQueryHandler _handler;

    public GetInfrastructureHealthQueryHandlerTests()
    {
        _mockHealthService = new Mock<IInfrastructureHealthService>();
        _mockLogger = new Mock<ILogger<GetInfrastructureHealthQueryHandler>>();
        _handler = new GetInfrastructureHealthQueryHandler(_mockHealthService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithServiceFilter_ReturnsSpecificServiceHealth()
    {
        // Arrange
        var query = new GetInfrastructureHealthQuery { ServiceName = "postgres" };

        var serviceHealth = new ServiceHealthStatus(
            "postgres",
            HealthState.Healthy,
            null,
            DateTime.UtcNow,
            TimeSpan.FromMilliseconds(50));

        var overallHealth = new OverallHealthStatus(
            HealthState.Healthy,
            4,
            4,
            0,
            0,
            DateTime.UtcNow);

        _mockHealthService
            .Setup(s => s.GetServiceHealthAsync("postgres", It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceHealth);

        _mockHealthService
            .Setup(s => s.GetOverallHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(overallHealth);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Healthy", result.Overall.State.ToString());
        Assert.Single(result.Services);
        Assert.Equal("postgres", result.Services.First().ServiceName);
        Assert.Equal("Healthy", result.Services.First().State);

        _mockHealthService.Verify(s => s.GetServiceHealthAsync("postgres", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithoutServiceFilter_ReturnsAllServicesHealth()
    {
        // Arrange
        var query = new GetInfrastructureHealthQuery { ServiceName = null };

        var services = new List<ServiceHealthStatus>
        {
            new("postgres", HealthState.Healthy, null, DateTime.UtcNow, TimeSpan.FromMilliseconds(50)),
            new("redis", HealthState.Healthy, null, DateTime.UtcNow, TimeSpan.FromMilliseconds(30)),
            new("qdrant", HealthState.Degraded, "Slow response", DateTime.UtcNow, TimeSpan.FromMilliseconds(800)),
            new("qdrant-collection", HealthState.Healthy, null, DateTime.UtcNow, TimeSpan.FromMilliseconds(100))
        };

        var overallHealth = new OverallHealthStatus(
            HealthState.Degraded, // Degraded because qdrant is degraded
            4,
            3,
            1,
            0,
            DateTime.UtcNow);

        _mockHealthService
            .Setup(s => s.GetAllServicesHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(services);

        _mockHealthService
            .Setup(s => s.GetOverallHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(overallHealth);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Degraded", result.Overall.State.ToString());
        Assert.Equal(4, result.Overall.TotalServices);
        Assert.Equal(3, result.Overall.HealthyServices);
        Assert.Equal(1, result.Overall.DegradedServices);
        Assert.Equal(0, result.Overall.UnhealthyServices);
        Assert.Equal(4, result.Services.Count);

        _mockHealthService.Verify(s => s.GetAllServicesHealthAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithUnhealthyServices_ReturnsUnhealthyOverall()
    {
        // Arrange
        var query = new GetInfrastructureHealthQuery();

        var services = new List<ServiceHealthStatus>
        {
            new("postgres", HealthState.Healthy, null, DateTime.UtcNow, TimeSpan.FromMilliseconds(50)),
            new("redis", HealthState.Unhealthy, "Connection refused", DateTime.UtcNow, TimeSpan.FromSeconds(5)),
            new("qdrant", HealthState.Healthy, null, DateTime.UtcNow, TimeSpan.FromMilliseconds(100))
        };

        var overallHealth = new OverallHealthStatus(
            HealthState.Unhealthy,
            3,
            2,
            0,
            1,
            DateTime.UtcNow);

        _mockHealthService
            .Setup(s => s.GetAllServicesHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(services);

        _mockHealthService
            .Setup(s => s.GetOverallHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(overallHealth);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Unhealthy", result.Overall.State.ToString());
        Assert.Equal(1, result.Overall.UnhealthyServices);

        var unhealthyService = result.Services.First(s => s.State == "Unhealthy");
        Assert.Equal("redis", unhealthyService.ServiceName);
        Assert.Equal("Connection refused", unhealthyService.ErrorMessage);
    }

    [Fact]
    public async Task Handle_WhenServiceThrows_PropagatesException()
    {
        // Arrange
        var query = new GetInfrastructureHealthQuery();

        _mockHealthService
            .Setup(s => s.GetOverallHealthAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Service unavailable"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(query, CancellationToken.None));
    }
}
