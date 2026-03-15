using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Infrastructure.HealthChecks;
using Api.Tests.Constants;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Infrastructure.HealthChecks;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserNotifications")]
public class SlackQueueHealthCheckTests
{
    private readonly Mock<INotificationQueueRepository> _repositoryMock;
    private readonly Mock<ILogger<SlackQueueHealthCheck>> _loggerMock;
    private readonly SlackQueueHealthCheck _healthCheck;

    public SlackQueueHealthCheckTests()
    {
        _repositoryMock = new Mock<INotificationQueueRepository>();
        _loggerMock = new Mock<ILogger<SlackQueueHealthCheck>>();

        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(sp => sp.GetService(typeof(INotificationQueueRepository)))
            .Returns(_repositoryMock.Object);

        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(serviceProvider.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

        _healthCheck = new SlackQueueHealthCheck(scopeFactory.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task CheckHealthAsync_PendingBelowThreshold_ReturnsHealthy()
    {
        // Arrange
        _repositoryMock.Setup(r => r.GetPendingCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(50);

        // Act
        var result = await _healthCheck.CheckHealthAsync(
            new HealthCheckContext(), CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("50", result.Description!);
    }

    [Fact]
    public async Task CheckHealthAsync_PendingAboveThreshold_ReturnsUnhealthy()
    {
        // Arrange
        _repositoryMock.Setup(r => r.GetPendingCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(150);

        // Act
        var result = await _healthCheck.CheckHealthAsync(
            new HealthCheckContext(), CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("150", result.Description!);
        Assert.Contains("backlog", result.Description!);
    }

    [Fact]
    public async Task CheckHealthAsync_ExactlyAtThreshold_ReturnsHealthy()
    {
        // Arrange
        _repositoryMock.Setup(r => r.GetPendingCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(100);

        // Act
        var result = await _healthCheck.CheckHealthAsync(
            new HealthCheckContext(), CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_RepositoryThrows_ReturnsUnhealthy()
    {
        // Arrange
        _repositoryMock.Setup(r => r.GetPendingCountAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB connection failed"));

        // Act
        var result = await _healthCheck.CheckHealthAsync(
            new HealthCheckContext(), CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("Failed to check", result.Description!);
    }

    [Fact]
    public async Task CheckHealthAsync_ZeroPending_ReturnsHealthy()
    {
        // Arrange
        _repositoryMock.Setup(r => r.GetPendingCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        var result = await _healthCheck.CheckHealthAsync(
            new HealthCheckContext(), CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("0", result.Description!);
    }
}
