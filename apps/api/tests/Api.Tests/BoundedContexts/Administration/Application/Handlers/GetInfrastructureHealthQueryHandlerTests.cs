using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

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
        result.Should().NotBeNull();
        result.Overall.State.ToString().Should().Be("Healthy");
        result.Services.Should().ContainSingle();
        result.Services.ElementAt(0).ServiceName.Should().Be("postgres");
        result.Services.ElementAt(0).State.Should().Be("Healthy");

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
        result.Should().NotBeNull();
        result.Overall.State.ToString().Should().Be("Degraded");
        result.Overall.TotalServices.Should().Be(4);
        result.Overall.HealthyServices.Should().Be(3);
        result.Overall.DegradedServices.Should().Be(1);
        result.Overall.UnhealthyServices.Should().Be(0);
        result.Services.Count.Should().Be(4);

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
        result.Should().NotBeNull();
        result.Overall.State.ToString().Should().Be("Unhealthy");
        result.Overall.UnhealthyServices.Should().Be(1);

        var unhealthyService = result.Services.First(s => s.State == "Unhealthy");
        unhealthyService.ServiceName.Should().Be("redis");
        unhealthyService.ErrorMessage.Should().Be("Connection refused");
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
        var act = () =>
            _handler.Handle(query, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>();
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
        var act = () =>
            _handler.Handle(query, CancellationToken.None);
        var exception = (await act.Should().ThrowAsync<TimeoutException>()).Which;

        exception.Message.Should().Be("Health check timed out after 5 seconds");
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
        var act = () =>
            _handler.Handle(query, CancellationToken.None);
        var exception = (await act.Should().ThrowAsync<TimeoutException>()).Which;

        exception.Message.Should().Be("Overall health check timed out");
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
        result.Should().NotBeNull();
        result.Services.Should().ContainSingle();
        var service = result.Services.First();
        service.ServiceName.Should().Be("slow-service");
        service.State.Should().Be("Degraded");
        service.ResponseTimeMs.Should().Be(4500.0);
        service.ErrorMessage.Should().Be("Slow response");
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
        var act = () =>
            _handler.Handle(query, CancellationToken.None);
        var exception = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;

        exception.Message.Should().Be("Failed to retrieve service list");
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
        var act = () =>
            _handler.Handle(query, CancellationToken.None);
        var exception = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;

        exception.Message.Should().Be("Service postgres not found");
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
        var act = () =>
            _handler.Handle(query, cancellationTokenSource.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // Issue #2911: Null Parameter Validation Tests
    [Fact]
    public void Constructor_WithNullHealthService_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () =>
            new GetInfrastructureHealthQueryHandler(null!, _mockLogger.Object);
        var exception = act.Should().Throw<ArgumentNullException>().Which;

        exception.ParamName.Should().Be("healthService");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () =>
            new GetInfrastructureHealthQueryHandler(_mockHealthService.Object, null!);
        var exception = act.Should().Throw<ArgumentNullException>().Which;

        exception.ParamName.Should().Be("logger");
    }

    [Fact]
    public async Task Handle_WithNullQuery_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () =>
            _handler.Handle(null!, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentNullException>();
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
        result.Should().NotBeNull();
        result.Services.Count.Should().Be(3);

        var healthyService = result.Services.First(s => s.ServiceName == "postgres");
        healthyService.State.Should().Be("Healthy");
        healthyService.ErrorMessage.Should().BeNull();

        var degradedService = result.Services.First(s => s.ServiceName == "redis");
        degradedService.State.Should().Be("Degraded");
        degradedService.ErrorMessage.Should().Be("High memory usage");

        var unhealthyService = result.Services.First(s => s.ServiceName == "qdrant");
        unhealthyService.State.Should().Be("Unhealthy");
        unhealthyService.ErrorMessage.Should().Be("Connection failed");
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
        result.Should().NotBeNull();
        result.Overall.State.ToString().Should().Be("Healthy");
        result.Overall.TotalServices.Should().Be(4);
        result.Overall.HealthyServices.Should().Be(4);
        result.Overall.DegradedServices.Should().Be(0);
        result.Overall.UnhealthyServices.Should().Be(0);
        result.Services.Should().AllSatisfy(service =>
        {
            service.State.Should().Be("Healthy");
            service.ErrorMessage.Should().BeNull();
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
        result.Should().NotBeNull();
        result.Services.Should().BeEmpty();
        result.Overall.TotalServices.Should().Be(0);
    }
}
