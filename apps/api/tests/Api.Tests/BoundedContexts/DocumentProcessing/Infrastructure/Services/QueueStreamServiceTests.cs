using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Infrastructure.Services;

[Trait("Category", TestCategories.Unit)]
public sealed class QueueStreamServiceTests
{
    private readonly QueueStreamService _service;

    public QueueStreamServiceTests()
    {
        var loggerMock = new Mock<ILogger<QueueStreamService>>();
        _service = new QueueStreamService(loggerMock.Object);
    }

    [Fact]
    public async Task PublishJobEvent_WithSubscriber_DeliversEvent()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var evt = new QueueStreamEvent(
            QueueStreamEventType.JobStarted, jobId,
            new JobStartedData(Guid.NewGuid(), "Processing"),
            DateTimeOffset.UtcNow);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var received = new List<QueueStreamEvent>();

        // Start subscriber in background
        var subscriberTask = Task.Run(async () =>
        {
            await foreach (var e in _service.SubscribeToJob(jobId, cts.Token))
            {
                received.Add(e);
                if (e.Type == QueueStreamEventType.JobCompleted)
                    break;
            }
        }, cts.Token);

        // Allow subscriber to register
        await Task.Delay(100, cts.Token);

        // Act - publish events
        await _service.PublishJobEventAsync(evt, cts.Token);

        var completedEvt = new QueueStreamEvent(
            QueueStreamEventType.JobCompleted, jobId,
            new JobCompletedData(5.0, null, null, null), DateTimeOffset.UtcNow);
        await _service.PublishJobEventAsync(completedEvt, cts.Token);

        await subscriberTask;

        // Assert
        received.Should().HaveCount(2);
        received[0].Type.Should().Be(QueueStreamEventType.JobStarted);
        received[1].Type.Should().Be(QueueStreamEventType.JobCompleted);
    }

    [Fact]
    public async Task PublishJobEvent_AlsoPublishesToQueueSubscribers()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var evt = new QueueStreamEvent(
            QueueStreamEventType.JobStarted, jobId,
            new JobStartedData(Guid.NewGuid(), "Processing"),
            DateTimeOffset.UtcNow);

        var queueReceived = new List<QueueStreamEvent>();

        // Start queue subscriber in background
        var subscriberTask = Task.Run(async () =>
        {
            await foreach (var e in _service.SubscribeToQueue(cts.Token))
            {
                queueReceived.Add(e);
#pragma warning disable S1751 // Intentional: retrieve only the first event then exit
                break; // Just get one event
#pragma warning restore S1751
            }
        }, cts.Token);

        await Task.Delay(100, cts.Token);

        // Act
        await _service.PublishJobEventAsync(evt, cts.Token);

        await subscriberTask;

        // Assert - job events should also go to queue subscribers
        queueReceived.Should().HaveCount(1);
        queueReceived[0].Type.Should().Be(QueueStreamEventType.JobStarted);
        queueReceived[0].JobId.Should().Be(jobId);
    }

    [Fact]
    public async Task PublishQueueEvent_DeliversOnlyToQueueSubscribers()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var evt = new QueueStreamEvent(
            QueueStreamEventType.QueueReordered, jobId,
            new QueueReorderedData(new List<Guid>()), DateTimeOffset.UtcNow);

        var queueReceived = new List<QueueStreamEvent>();

        // Start queue subscriber
        var queueTask = Task.Run(async () =>
        {
            await foreach (var e in _service.SubscribeToQueue(cts.Token))
            {
                queueReceived.Add(e);
#pragma warning disable S1751 // Intentional: retrieve only the first event then exit
                break;
#pragma warning restore S1751
            }
        }, cts.Token);

        await Task.Delay(100, cts.Token);

        // Act
        await _service.PublishQueueEventAsync(evt, cts.Token);

        await queueTask;

        // Assert
        queueReceived.Should().HaveCount(1);
        queueReceived[0].Type.Should().Be(QueueStreamEventType.QueueReordered);
    }

    [Fact]
    public async Task SubscribeToJob_AutoCompletes_OnTerminalEvents()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var received = new List<QueueStreamEvent>();

        var subscriberTask = Task.Run(async () =>
        {
            await foreach (var e in _service.SubscribeToJob(jobId, cts.Token))
            {
                received.Add(e);
            }
        }, cts.Token);

        await Task.Delay(100, cts.Token);

        // Act - publish a terminal event (JobFailed)
        var failedEvt = new QueueStreamEvent(
            QueueStreamEventType.JobFailed, jobId,
            new JobFailedData("Test error", null, 0, null, null, null), DateTimeOffset.UtcNow);
        await _service.PublishJobEventAsync(failedEvt, cts.Token);

        await subscriberTask;

        // Assert - subscriber auto-completes after terminal event
        received.Should().HaveCount(1);
        received[0].Type.Should().Be(QueueStreamEventType.JobFailed);
    }

    [Fact]
    public async Task PublishJobEvent_NoSubscribers_DoesNotThrow()
    {
        // Arrange
        var evt = new QueueStreamEvent(
            QueueStreamEventType.JobStarted, Guid.NewGuid(),
            new JobStartedData(Guid.NewGuid(), "Processing"),
            DateTimeOffset.UtcNow);

        // Act & Assert - should not throw even with no subscribers
        var act = () => _service.PublishJobEventAsync(evt, CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SubscribeToJob_CleansUp_WhenCancelled()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();

        var subscriberTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var _ in _service.SubscribeToJob(jobId, cts.Token))
                {
                    // Will never receive events before cancellation
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation token is triggered
            }
        }, CancellationToken.None);

        await Task.Delay(100);

        // Act - cancel the subscription
        await cts.CancelAsync();

        // Assert - subscriber task completes cleanly
        var act = () => subscriberTask;
        await act.Should().NotThrowAsync();
    }
}
