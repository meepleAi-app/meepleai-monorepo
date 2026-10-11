using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Infrastructure.Services;

/// <summary>
/// <c>SubscribeToJob</c> e <c>SubscribeToQueue</c> sono iteratori lazy: il sottoscrittore si registra
/// alla prima <c>MoveNextAsync</c> del consumer, in un momento deciso dal thread pool. Questi test
/// pubblicavano dopo un <c>Task.Delay(100)</c> che provava a indovinarlo; sotto il carico della
/// suite la pubblicazione arrivava prima della registrazione e l'evento andava perso (#4193).
/// Ora si sincronizzano sul numero di sottoscrittori, e la vita dei sottoscrittori la decide il
/// test, non un orologio: il limite di <see cref="SafetyNet"/> scatta solo se qualcosa si blocca.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class QueueStreamServiceTests
{
    /// <summary>Limite superiore per non appendere la suite. Mai sul percorso che passa.</summary>
    private static readonly TimeSpan SafetyNet = TimeSpan.FromSeconds(30);

    private readonly QueueStreamService _service;

    public QueueStreamServiceTests()
    {
        var loggerMock = new Mock<ILogger<QueueStreamService>>();
        _service = new QueueStreamService(loggerMock.Object);
    }

    private static async Task WaitForCountAsync(Func<int> count, int expected, string what)
    {
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        int actual;
        while ((actual = count()) != expected)
        {
            if (elapsed.Elapsed > SafetyNet)
            {
                throw new TimeoutException(
                    $"{what}: attesi {expected} sottoscrittori entro {SafetyNet.TotalSeconds:0}s, osservati {actual}.");
            }

            await Task.Delay(5);
        }
    }

    private Task WaitForJobSubscribersAsync(Guid jobId, int expected) =>
        WaitForCountAsync(() => _service.GetJobSubscriberCount(jobId), expected, $"job {jobId}");

    private Task WaitForQueueSubscribersAsync(int expected) =>
        WaitForCountAsync(() => _service.QueueSubscriberCount, expected, "coda");

    [Fact]
    public async Task PublishJobEvent_WithSubscriber_DeliversEvent()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var evt = new QueueStreamEvent(
            QueueStreamEventType.JobStarted, jobId,
            new JobStartedData(Guid.NewGuid(), "Processing"),
            DateTimeOffset.UtcNow);

        using var cts = new CancellationTokenSource();
        var received = new List<QueueStreamEvent>();

        var subscriberTask = Task.Run(async () =>
        {
            await foreach (var e in _service.SubscribeToJob(jobId, cts.Token))
            {
                received.Add(e);
                if (e.Type == QueueStreamEventType.JobCompleted)
                    break;
            }
        }, cts.Token);

        await WaitForJobSubscribersAsync(jobId, 1);

        // Act - publish events
        await _service.PublishJobEventAsync(evt, cts.Token);

        var completedEvt = new QueueStreamEvent(
            QueueStreamEventType.JobCompleted, jobId,
            new JobCompletedData(5.0, null, null, null), DateTimeOffset.UtcNow);
        await _service.PublishJobEventAsync(completedEvt, cts.Token);

        await subscriberTask.WaitAsync(SafetyNet);

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

        using var cts = new CancellationTokenSource();
        var queueReceived = new List<QueueStreamEvent>();

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

        await WaitForQueueSubscribersAsync(1);

        // Act
        await _service.PublishJobEventAsync(evt, cts.Token);

        await subscriberTask.WaitAsync(SafetyNet);

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

        using var cts = new CancellationTokenSource();
        var queueReceived = new List<QueueStreamEvent>();

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

        await WaitForQueueSubscribersAsync(1);

        // Act
        await _service.PublishQueueEventAsync(evt, cts.Token);

        await queueTask.WaitAsync(SafetyNet);

        // Assert
        queueReceived.Should().HaveCount(1);
        queueReceived[0].Type.Should().Be(QueueStreamEventType.QueueReordered);
    }

    [Fact]
    public async Task SubscribeToJob_AutoCompletes_OnTerminalEvents()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();
        var received = new List<QueueStreamEvent>();

        var subscriberTask = Task.Run(async () =>
        {
            await foreach (var e in _service.SubscribeToJob(jobId, cts.Token))
            {
                received.Add(e);
            }
        }, cts.Token);

        await WaitForJobSubscribersAsync(jobId, 1);

        // Act - publish a terminal event (JobFailed)
        var failedEvt = new QueueStreamEvent(
            QueueStreamEventType.JobFailed, jobId,
            new JobFailedData("Test error", null, 0, null, null, null), DateTimeOffset.UtcNow);
        await _service.PublishJobEventAsync(failedEvt, cts.Token);

        // The token is never cancelled: if the subscription did not complete by itself on the
        // terminal event, this would hit the safety net instead of passing.
        await subscriberTask.WaitAsync(SafetyNet);

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

        // Cancel a subscriber that is actually registered: before #4193 the test cancelled after a
        // fixed delay, possibly before registration, and never checked that anything was removed.
        await WaitForJobSubscribersAsync(jobId, 1);

        // Act - cancel the subscription
        await cts.CancelAsync();

        // Assert - subscriber task completes cleanly, and the job has no subscriber left
        var act = () => subscriberTask.WaitAsync(SafetyNet);
        await act.Should().NotThrowAsync();
        _service.GetJobSubscriberCount(jobId).Should().Be(0);
    }
}
