using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.BoundedContexts.UserNotifications.Infrastructure.HealthChecks;
using Api.Tests.Constants;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

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

    /// <summary>
    /// Sets up the channel-scoped pending count that the (fixed) health check must consume.
    /// </summary>
    private void SetupSlackPendingCount(int count)
    {
        _repositoryMock.Setup(r => r.GetPendingCountByChannelsAsync(
                It.IsAny<IReadOnlyCollection<NotificationChannelType>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(count);
    }

    [Fact]
    public async Task CheckHealthAsync_PendingBelowThreshold_ReturnsHealthy()
    {
        // Arrange
        SetupSlackPendingCount(50);

        // Act
        var result = await _healthCheck.CheckHealthAsync(
            new HealthCheckContext(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description!.Should().Contain("50");
    }

    [Fact]
    public async Task CheckHealthAsync_PendingAboveThreshold_ReturnsUnhealthy()
    {
        // Arrange
        SetupSlackPendingCount(150);

        // Act
        var result = await _healthCheck.CheckHealthAsync(
            new HealthCheckContext(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description!.Should().Contain("150");
        result.Description!.Should().Contain("backlog");
    }

    [Fact]
    public async Task CheckHealthAsync_ExactlyAtThreshold_ReturnsHealthy()
    {
        // Arrange
        SetupSlackPendingCount(100);

        // Act
        var result = await _healthCheck.CheckHealthAsync(
            new HealthCheckContext(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_RepositoryThrows_ReturnsUnhealthy()
    {
        // Arrange
        _repositoryMock.Setup(r => r.GetPendingCountByChannelsAsync(
                It.IsAny<IReadOnlyCollection<NotificationChannelType>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB connection failed"));

        // Act
        var result = await _healthCheck.CheckHealthAsync(
            new HealthCheckContext(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description!.Should().Contain("Failed to check");
    }

    [Fact]
    public async Task CheckHealthAsync_ZeroPending_ReturnsHealthy()
    {
        // Arrange
        SetupSlackPendingCount(0);

        // Act
        var result = await _healthCheck.CheckHealthAsync(
            new HealthCheckContext(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description!.Should().Contain("0");
    }

    /// <summary>
    /// Regression for the mis-attributed 503 on staging: a large backlog on a NON-Slack
    /// channel (310 pending email items) must NOT trip the Slack queue health check.
    /// The check must count only Slack-channel pending items — here zero — and report Healthy.
    /// Before the fix the check called the unfiltered <c>GetPendingCountAsync</c> and returned
    /// Unhealthy (the aggregate /health then returned 503, blamed on Slack).
    /// </summary>
    [Fact]
    public async Task CheckHealthAsync_OtherChannelBacklog_IgnoresNonSlackAndReturnsHealthy()
    {
        // Arrange — 310 pending items exist overall (the staging email backlog) but ZERO
        // belong to a Slack channel.
        _repositoryMock.Setup(r => r.GetPendingCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(310);
        SetupSlackPendingCount(0);

        // Act
        var result = await _healthCheck.CheckHealthAsync(
            new HealthCheckContext(), CancellationToken.None);

        // Assert — the Slack check ignores the email backlog and stays Healthy
        result.Status.Should().Be(HealthStatus.Healthy);
    }

    /// <summary>
    /// The health check must query the channel-scoped count restricted to the two Slack
    /// channels (slack_user + slack_team), never the unfiltered all-channel count.
    /// </summary>
    [Fact]
    public async Task CheckHealthAsync_QueriesOnlySlackChannels()
    {
        // Arrange
        SetupSlackPendingCount(5);

        // Act
        await _healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        // Assert — the Slack channels (and only those) were passed to the filtered query
        _repositoryMock.Verify(r => r.GetPendingCountByChannelsAsync(
            It.Is<IReadOnlyCollection<NotificationChannelType>>(c =>
                c.Contains(NotificationChannelType.SlackUser)
                && c.Contains(NotificationChannelType.SlackTeam)
                && !c.Contains(NotificationChannelType.Email)),
            It.IsAny<CancellationToken>()), Times.Once);
        _repositoryMock.Verify(r => r.GetPendingCountAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
