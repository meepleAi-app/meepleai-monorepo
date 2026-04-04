using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.EventHandlers;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.EventHandlers;

[Trait("Category", TestCategories.Unit)]
public sealed class QueueStreamEventHandlersTests : IDisposable
{
    private readonly Mock<IQueueStreamService> _streamServiceMock = new();
    private readonly MeepleAiDbContext _db;

    public QueueStreamEventHandlersTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: $"QueueStreamTests_{Guid.NewGuid()}")
            .Options;
        _db = new MeepleAiDbContext(options, Mock.Of<IMediator>(), Mock.Of<IDomainEventCollector>());
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task JobQueuedStreamHandler_PublishesJobQueuedEvent()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<JobQueuedStreamHandler>>();
        var handler = new JobQueuedStreamHandler(_streamServiceMock.Object, loggerMock.Object);
        var notification = new JobQueuedEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 5);

        QueueStreamEvent? captured = null;
        _streamServiceMock
            .Setup(s => s.PublishJobEventAsync(It.IsAny<QueueStreamEvent>(), It.IsAny<CancellationToken>()))
            .Callback<QueueStreamEvent, CancellationToken>((e, _) => captured = e)
            .Returns(Task.CompletedTask);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        _streamServiceMock.Verify(
            s => s.PublishJobEventAsync(It.IsAny<QueueStreamEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
        captured.Should().NotBeNull();
        captured!.Type.Should().Be(QueueStreamEventType.JobQueued);
        captured.JobId.Should().Be(notification.JobId);
        captured.Data.Should().BeOfType<JobQueuedData>();
    }

    [Fact]
    public async Task JobStartedStreamHandler_PublishesJobStartedEvent()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<JobStartedStreamHandler>>();
        var handler = new JobStartedStreamHandler(_streamServiceMock.Object, loggerMock.Object);
        var notification = new JobStartedEvent(Guid.NewGuid(), Guid.NewGuid());

        QueueStreamEvent? captured = null;
        _streamServiceMock
            .Setup(s => s.PublishJobEventAsync(It.IsAny<QueueStreamEvent>(), It.IsAny<CancellationToken>()))
            .Callback<QueueStreamEvent, CancellationToken>((e, _) => captured = e)
            .Returns(Task.CompletedTask);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        captured.Should().NotBeNull();
        captured!.Type.Should().Be(QueueStreamEventType.JobStarted);
        captured.JobId.Should().Be(notification.JobId);
    }

    [Fact]
    public async Task JobStepCompletedStreamHandler_PublishesStepCompletedEvent()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<JobStepCompletedStreamHandler>>();
        var handler = new JobStepCompletedStreamHandler(_streamServiceMock.Object, loggerMock.Object);
        var notification = new JobStepCompletedEvent(
            Guid.NewGuid(), Guid.NewGuid(), ProcessingStepType.Extract, TimeSpan.FromSeconds(12.5));

        QueueStreamEvent? captured = null;
        _streamServiceMock
            .Setup(s => s.PublishJobEventAsync(It.IsAny<QueueStreamEvent>(), It.IsAny<CancellationToken>()))
            .Callback<QueueStreamEvent, CancellationToken>((e, _) => captured = e)
            .Returns(Task.CompletedTask);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        captured.Should().NotBeNull();
        captured!.Type.Should().Be(QueueStreamEventType.StepCompleted);
        var data = captured.Data.Should().BeOfType<StepCompletedData>().Which;
        data.Step.Should().Be("Extract");
        data.DurationSeconds.Should().Be(12.5);
    }

    [Fact]
    public async Task JobCompletedStreamHandler_PublishesJobCompletedEvent()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<JobCompletedStreamHandler>>();
        var handler = new JobCompletedStreamHandler(_streamServiceMock.Object, _db, loggerMock.Object);
        var notification = new JobCompletedEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromSeconds(45));

        QueueStreamEvent? captured = null;
        _streamServiceMock
            .Setup(s => s.PublishJobEventAsync(It.IsAny<QueueStreamEvent>(), It.IsAny<CancellationToken>()))
            .Callback<QueueStreamEvent, CancellationToken>((e, _) => captured = e)
            .Returns(Task.CompletedTask);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        captured.Should().NotBeNull();
        captured!.Type.Should().Be(QueueStreamEventType.JobCompleted);
        var data = captured.Data.Should().BeOfType<JobCompletedData>().Which;
        data.TotalDurationSeconds.Should().Be(45);
    }

    [Fact]
    public async Task JobFailedStreamHandler_PublishesJobFailedEvent()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<JobFailedStreamHandler>>();
        var handler = new JobFailedStreamHandler(_streamServiceMock.Object, _db, loggerMock.Object);
        var notification = new JobFailedEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Extract failed", ProcessingStepType.Extract, 2);

        QueueStreamEvent? captured = null;
        _streamServiceMock
            .Setup(s => s.PublishJobEventAsync(It.IsAny<QueueStreamEvent>(), It.IsAny<CancellationToken>()))
            .Callback<QueueStreamEvent, CancellationToken>((e, _) => captured = e)
            .Returns(Task.CompletedTask);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        captured.Should().NotBeNull();
        captured!.Type.Should().Be(QueueStreamEventType.JobFailed);
        var data = captured.Data.Should().BeOfType<JobFailedData>().Which;
        data.Error.Should().Be("Extract failed");
        data.FailedAtStep.Should().Be("Extract");
        data.RetryCount.Should().Be(2);
    }

    [Fact]
    public async Task JobCancelledStreamHandler_PublishesJobRemovedEvent()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<JobCancelledStreamHandler>>();
        var handler = new JobCancelledStreamHandler(_streamServiceMock.Object, loggerMock.Object);
        var notification = new JobCancelledEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        QueueStreamEvent? captured = null;
        _streamServiceMock
            .Setup(s => s.PublishJobEventAsync(It.IsAny<QueueStreamEvent>(), It.IsAny<CancellationToken>()))
            .Callback<QueueStreamEvent, CancellationToken>((e, _) => captured = e)
            .Returns(Task.CompletedTask);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        captured.Should().NotBeNull();
        captured!.Type.Should().Be(QueueStreamEventType.JobRemoved);
        var data = captured.Data.Should().BeOfType<JobRemovedData>().Which;
        data.Reason.Should().Be("Cancelled");
    }

    [Fact]
    public async Task JobPriorityChangedStreamHandler_PublishesQueueReorderedEvent()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<JobPriorityChangedStreamHandler>>();
        var handler = new JobPriorityChangedStreamHandler(_streamServiceMock.Object, loggerMock.Object);
        var notification = new JobPriorityChangedEvent(Guid.NewGuid(), 0, 5);

        _streamServiceMock
            .Setup(s => s.PublishQueueEventAsync(It.IsAny<QueueStreamEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert - publishes to queue (not job)
        _streamServiceMock.Verify(
            s => s.PublishQueueEventAsync(It.Is<QueueStreamEvent>(e => e.Type == QueueStreamEventType.QueueReordered), It.IsAny<CancellationToken>()),
            Times.Once);
        _streamServiceMock.Verify(
            s => s.PublishJobEventAsync(It.IsAny<QueueStreamEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task JobRetriedStreamHandler_PublishesJobRetriedEvent()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<JobRetriedStreamHandler>>();
        var handler = new JobRetriedStreamHandler(_streamServiceMock.Object, loggerMock.Object);
        var notification = new JobRetriedEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 3);

        QueueStreamEvent? captured = null;
        _streamServiceMock
            .Setup(s => s.PublishJobEventAsync(It.IsAny<QueueStreamEvent>(), It.IsAny<CancellationToken>()))
            .Callback<QueueStreamEvent, CancellationToken>((e, _) => captured = e)
            .Returns(Task.CompletedTask);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        captured.Should().NotBeNull();
        captured!.Type.Should().Be(QueueStreamEventType.JobRetried);
        var data = captured.Data.Should().BeOfType<JobRetriedData>().Which;
        data.RetryCount.Should().Be(3);
    }
}
