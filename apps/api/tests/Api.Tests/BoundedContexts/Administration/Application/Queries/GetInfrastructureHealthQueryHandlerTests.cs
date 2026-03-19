using Api.BoundedContexts.Administration.Application.Handlers;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Queries;

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
        Assert.Equal("postgres", result.Services.ElementAt(0).ServiceName);
        Assert.Equal("Healthy", result.Services.ElementAt(0).State);

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

    // Issue #2911: Timeout Scenario Tests
    [Fact]
    public async Task Handle_WhenServiceHealthCheckTimesOut_ThrowsTimeoutException()
    {
        // Arrange
        var query = new GetInfrastructureHealthQuery { ServiceName = "slow-service" };

        _mockHealthService
            .Setup(s => s.GetOverallHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OverallHealthStatus(
                HealthState.Healthy,
                1,
                1,
                0,
                0,
                DateTime.UtcNow));

        _mockHealthService
            .Setup(s => s.GetServiceHealthAsync("slow-service", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Health check timed out after 5 seconds"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<TimeoutException>(() =>
            _handler.Handle(query, CancellationToken.None));

        Assert.Equal("Health check timed out after 5 seconds", exception.Message);
    }

    [Fact]
    public async Task Handle_WhenOverallHealthCheckTimesOut_ThrowsTimeoutException()
    {
        // Arrange
        var query = new GetInfrastructureHealthQuery();

        _mockHealthService
            .Setup(s => s.GetOverallHealthAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Overall health check timed out"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<TimeoutException>(() =>
            _handler.Handle(query, CancellationToken.None));

        Assert.Equal("Overall health check timed out", exception.Message);
    }

    [Fact]
    public async Task Handle_WithSlowService_ReturnsHealthWithLongResponseTime()
    {
        // Arrange
        var query = new GetInfrastructureHealthQuery { ServiceName = "slow-service" };

        var slowServiceHealth = new ServiceHealthStatus(
            "slow-service",
            HealthState.Degraded,
            "Slow response",
            DateTime.UtcNow,
            TimeSpan.FromSeconds(4.5));

        var overallHealth = new OverallHealthStatus(
            HealthState.Degraded,
            1,
            0,
            1,
            0,
            DateTime.UtcNow);

        _mockHealthService
            .Setup(s => s.GetServiceHealthAsync("slow-service", It.IsAny<CancellationToken>()))
            .ReturnsAsync(slowServiceHealth);

        _mockHealthService
            .Setup(s => s.GetOverallHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(overallHealth);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Services);
        var service = result.Services.First();
        Assert.Equal("slow-service", service.ServiceName);
        Assert.Equal("Degraded", service.State);
        Assert.Equal(4500.0, service.ResponseTimeMs);
        Assert.Equal("Slow response", service.ErrorMessage);
    }

    // Issue #2911: Enhanced Error Handling Tests
    [Fact]
    public async Task Handle_WhenGetAllServicesThrows_PropagatesException()
    {
        // Arrange
        var query = new GetInfrastructureHealthQuery();

        _mockHealthService
            .Setup(s => s.GetOverallHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OverallHealthStatus(
                HealthState.Healthy,
                0,
                0,
                0,
                0,
                DateTime.UtcNow));

        _mockHealthService
            .Setup(s => s.GetAllServicesHealthAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Failed to retrieve service list"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(query, CancellationToken.None));

        Assert.Equal("Failed to retrieve service list", exception.Message);
    }

    [Fact]
    public async Task Handle_WhenGetSpecificServiceThrows_PropagatesException()
    {
        // Arrange
        var query = new GetInfrastructureHealthQuery { ServiceName = "postgres" };

        _mockHealthService
            .Setup(s => s.GetOverallHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OverallHealthStatus(
                HealthState.Healthy,
                1,
                1,
                0,
                0,
                DateTime.UtcNow));

        _mockHealthService
            .Setup(s => s.GetServiceHealthAsync("postgres", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Service postgres not found"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(query, CancellationToken.None));

        Assert.Equal("Service postgres not found", exception.Message);
    }

    [Fact]
    public async Task Handle_WhenCancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var query = new GetInfrastructureHealthQuery();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        _mockHealthService
            .Setup(s => s.GetOverallHealthAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _handler.Handle(query, cancellationTokenSource.Token));
    }

    // Issue #2911: Null Parameter Validation Tests
    [Fact]
    public void Constructor_WithNullHealthService_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new GetInfrastructureHealthQueryHandler(null!, _mockLogger.Object));

        Assert.Equal("healthService", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new GetInfrastructureHealthQueryHandler(_mockHealthService.Object, null!));

        Assert.Equal("logger", exception.ParamName);
    }

    [Fact]
    public async Task Handle_WithNullQuery_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _handler.Handle(null!, CancellationToken.None));
    }

    // Issue #2911: Service Status Mapping Tests
    [Fact]
    public async Task Handle_WithMultipleHealthStates_MapsAllStatesCorrectly()
    {
        // Arrange
        var query = new GetInfrastructureHealthQuery();

        var services = new List<ServiceHealthStatus>
        {
            new("postgres", HealthState.Healthy, null, DateTime.UtcNow, TimeSpan.FromMilliseconds(50)),
            new("redis", HealthState.Degraded, "High memory usage", DateTime.UtcNow, TimeSpan.FromMilliseconds(150)),
            new("qdrant", HealthState.Unhealthy, "Connection failed", DateTime.UtcNow, TimeSpan.FromSeconds(5))
        };

        var overallHealth = new OverallHealthStatus(
            HealthState.Unhealthy,
            3,
            1,
            1,
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
        Assert.Equal(3, result.Services.Count);

        var healthyService = result.Services.First(s => s.ServiceName == "postgres");
        Assert.Equal("Healthy", healthyService.State);
        Assert.Null(healthyService.ErrorMessage);

        var degradedService = result.Services.First(s => s.ServiceName == "redis");
        Assert.Equal("Degraded", degradedService.State);
        Assert.Equal("High memory usage", degradedService.ErrorMessage);

        var unhealthyService = result.Services.First(s => s.ServiceName == "qdrant");
        Assert.Equal("Unhealthy", unhealthyService.State);
        Assert.Equal("Connection failed", unhealthyService.ErrorMessage);
    }

    [Fact]
    public async Task Handle_WithAllHealthyServices_ReturnsHealthyOverall()
    {
        // Arrange
        var query = new GetInfrastructureHealthQuery();

        var services = new List<ServiceHealthStatus>
        {
            new("postgres", HealthState.Healthy, null, DateTime.UtcNow, TimeSpan.FromMilliseconds(50)),
            new("redis", HealthState.Healthy, null, DateTime.UtcNow, TimeSpan.FromMilliseconds(30)),
            new("qdrant", HealthState.Healthy, null, DateTime.UtcNow, TimeSpan.FromMilliseconds(100)),
            new("qdrant-collection", HealthState.Healthy, null, DateTime.UtcNow, TimeSpan.FromMilliseconds(80))
        };

        var overallHealth = new OverallHealthStatus(
            HealthState.Healthy,
            4,
            4,
            0,
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
        Assert.Equal("Healthy", result.Overall.State.ToString());
        Assert.Equal(4, result.Overall.TotalServices);
        Assert.Equal(4, result.Overall.HealthyServices);
        Assert.Equal(0, result.Overall.DegradedServices);
        Assert.Equal(0, result.Overall.UnhealthyServices);
        Assert.All(result.Services, service =>
        {
            Assert.Equal("Healthy", service.State);
            Assert.Null(service.ErrorMessage);
        });
    }

    [Fact]
    public async Task Handle_WithEmptyServiceList_ReturnsEmptyServices()
    {
        // Arrange
        var query = new GetInfrastructureHealthQuery();

        var overallHealth = new OverallHealthStatus(
            HealthState.Healthy,
            0,
            0,
            0,
            0,
            DateTime.UtcNow);

        _mockHealthService
            .Setup(s => s.GetAllServicesHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ServiceHealthStatus>());

        _mockHealthService
            .Setup(s => s.GetOverallHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(overallHealth);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Services);
        Assert.Equal(0, result.Overall.TotalServices);
    }
}
