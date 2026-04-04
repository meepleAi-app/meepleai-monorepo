using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Quartz;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Unit tests for TriggerModelAvailabilityCheckCommandHandler.
/// Issue #5503: Part of Epic #5490 - Model Versioning &amp; Availability Monitoring.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class TriggerModelAvailabilityCheckCommandHandlerTests
{
    private readonly Mock<ISchedulerFactory> _schedulerFactoryMock;
    private readonly Mock<IScheduler> _schedulerMock;
    private readonly TriggerModelAvailabilityCheckCommandHandler _handler;

    public TriggerModelAvailabilityCheckCommandHandlerTests()
    {
        _schedulerFactoryMock = new Mock<ISchedulerFactory>();
        _schedulerMock = new Mock<IScheduler>();
        _schedulerFactoryMock.Setup(f => f.GetScheduler(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_schedulerMock.Object);

        _handler = new TriggerModelAvailabilityCheckCommandHandler(
            _schedulerFactoryMock.Object,
            Mock.Of<ILogger<TriggerModelAvailabilityCheckCommandHandler>>());
    }

    [Fact]
    public async Task Handle_WhenJobExists_TriggersJob()
    {
        // Arrange
        var jobKey = new JobKey("model-availability-check-job", "knowledge-base");
        _schedulerMock.Setup(s => s.CheckExists(jobKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _handler.Handle(
            new TriggerModelAvailabilityCheckCommand(), CancellationToken.None);

        // Assert
        result.Triggered.Should().BeTrue();
        result.Message.Should().Contain("triggered");
        _schedulerMock.Verify(s => s.TriggerJob(jobKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenJobNotFound_ReturnsFalse()
    {
        // Arrange
        var jobKey = new JobKey("model-availability-check-job", "knowledge-base");
        _schedulerMock.Setup(s => s.CheckExists(jobKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _handler.Handle(
            new TriggerModelAvailabilityCheckCommand(), CancellationToken.None);

        // Assert
        result.Triggered.Should().BeFalse();
        result.Message.Should().Contain("not found");
        _schedulerMock.Verify(s => s.TriggerJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
