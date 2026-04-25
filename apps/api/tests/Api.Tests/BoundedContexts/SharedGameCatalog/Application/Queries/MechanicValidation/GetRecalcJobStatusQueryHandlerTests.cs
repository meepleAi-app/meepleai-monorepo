using Api.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicValidation;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using System.Globalization;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicValidation;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class GetRecalcJobStatusQueryHandlerTests
{
    private readonly Mock<IMechanicRecalcJobRepository> _jobRepoMock = new();
    private readonly Mock<ILogger<GetRecalcJobStatusQueryHandler>> _loggerMock = new();
    private readonly FakeTimeProvider _timeProvider = new(
        DateTimeOffset.Parse("2026-04-25T12:00:00Z", CultureInfo.InvariantCulture));

    private readonly GetRecalcJobStatusQueryHandler _handler;

    public GetRecalcJobStatusQueryHandlerTests()
    {
        _handler = new GetRecalcJobStatusQueryHandler(
            _jobRepoMock.Object,
            _timeProvider,
            _loggerMock.Object);
    }

    [Fact]
    public void Constructor_WithNullJobRepository_Throws()
    {
        var act = () => new GetRecalcJobStatusQueryHandler(
            jobRepository: null!,
            _timeProvider,
            _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("jobRepository");
    }

    [Fact]
    public void Constructor_WithNullTimeProvider_Throws()
    {
        var act = () => new GetRecalcJobStatusQueryHandler(
            _jobRepoMock.Object,
            timeProvider: null!,
            _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("timeProvider");
    }

    [Fact]
    public void Constructor_WithNullLogger_Throws()
    {
        var act = () => new GetRecalcJobStatusQueryHandler(
            _jobRepoMock.Object,
            _timeProvider,
            logger: null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public async Task Handle_WithNullRequest_Throws()
    {
        var act = () => _handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("request");
    }

    [Fact]
    public async Task Handle_JobNotFound_ThrowsNotFound()
    {
        var jobId = Guid.NewGuid();
        _jobRepoMock
            .Setup(r => r.GetByIdAsync(jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MechanicRecalcJob?)null);

        var act = () => _handler.Handle(
            new GetRecalcJobStatusQuery(jobId),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_PendingJob_MapsScalarsAndReturnsNullEta()
    {
        var userId = Guid.NewGuid();
        var job = MechanicRecalcJob.Enqueue(userId);
        _jobRepoMock
            .Setup(r => r.GetByIdAsync(job.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        var dto = await _handler.Handle(
            new GetRecalcJobStatusQuery(job.Id),
            TestContext.Current.CancellationToken);

        dto.Id.Should().Be(job.Id);
        dto.Status.Should().Be(RecalcJobStatus.Pending);
        dto.TriggeredByUserId.Should().Be(userId);
        dto.Total.Should().Be(0);
        dto.Processed.Should().Be(0);
        dto.Failed.Should().Be(0);
        dto.Skipped.Should().Be(0);
        dto.ConsecutiveFailures.Should().Be(0);
        dto.LastError.Should().BeNull();
        dto.CancellationRequested.Should().BeFalse();
        dto.CreatedAt.Should().Be(job.CreatedAt);
        dto.StartedAt.Should().BeNull();
        dto.CompletedAt.Should().BeNull();
        dto.HeartbeatAt.Should().BeNull();
        dto.EtaSeconds.Should().BeNull("ETA is only computed for Running jobs with progress");
    }

    [Fact]
    public async Task Handle_RunningJob_NoProgress_ReturnsNullEta()
    {
        var job = BuildRunningJob(processed: 0, total: 10, startedSecondsAgo: 30);

        _jobRepoMock
            .Setup(r => r.GetByIdAsync(job.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        var dto = await _handler.Handle(
            new GetRecalcJobStatusQuery(job.Id),
            TestContext.Current.CancellationToken);

        dto.EtaSeconds.Should().BeNull("a job that has not processed any item yet has no rate to extrapolate from");
    }

    [Fact]
    public async Task Handle_RunningJob_WithProgress_ComputesEtaFromObservedThroughput()
    {
        // 30 seconds elapsed, 10 of 100 processed => avg 3s per item, 90 remaining => 270s ETA.
        var job = BuildRunningJob(processed: 10, total: 100, startedSecondsAgo: 30);

        _jobRepoMock
            .Setup(r => r.GetByIdAsync(job.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        var dto = await _handler.Handle(
            new GetRecalcJobStatusQuery(job.Id),
            TestContext.Current.CancellationToken);

        dto.Status.Should().Be(RecalcJobStatus.Running);
        dto.Processed.Should().Be(10);
        dto.Total.Should().Be(100);
        dto.EtaSeconds.Should().NotBeNull();
        dto.EtaSeconds!.Value.Should().BeApproximately(270d, 0.001);
    }

    [Fact]
    public async Task Handle_RunningJob_AllProcessed_EtaIsZero()
    {
        // Edge case: worker has processed every candidate but hasn't called Complete() yet.
        var job = BuildRunningJob(processed: 100, total: 100, startedSecondsAgo: 60);

        _jobRepoMock
            .Setup(r => r.GetByIdAsync(job.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        var dto = await _handler.Handle(
            new GetRecalcJobStatusQuery(job.Id),
            TestContext.Current.CancellationToken);

        dto.EtaSeconds.Should().Be(0d, "no items remain so the worker is about to call Complete()");
    }

    [Fact]
    public async Task Handle_CompletedJob_ReturnsNullEtaAndPopulatesCompletedAt()
    {
        var job = MechanicRecalcJob.Enqueue(Guid.NewGuid());
        job.MarkRunning(total: 5);
        job.RecordSuccess(Guid.NewGuid());
        job.Heartbeat();
        job.Complete();

        _jobRepoMock
            .Setup(r => r.GetByIdAsync(job.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        var dto = await _handler.Handle(
            new GetRecalcJobStatusQuery(job.Id),
            TestContext.Current.CancellationToken);

        dto.Status.Should().Be(RecalcJobStatus.Completed);
        dto.CompletedAt.Should().NotBeNull();
        dto.EtaSeconds.Should().BeNull("ETA is only computed while Running");
    }

    [Fact]
    public async Task Handle_FailedJob_ReturnsNullEtaAndSurfacesLastError()
    {
        var job = MechanicRecalcJob.Enqueue(Guid.NewGuid());
        job.Fail("circuit breaker opened");

        _jobRepoMock
            .Setup(r => r.GetByIdAsync(job.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        var dto = await _handler.Handle(
            new GetRecalcJobStatusQuery(job.Id),
            TestContext.Current.CancellationToken);

        dto.Status.Should().Be(RecalcJobStatus.Failed);
        dto.LastError.Should().Be("circuit breaker opened");
        dto.EtaSeconds.Should().BeNull();
    }

    [Fact]
    public async Task Handle_PreservesCancellationRequestedFlag()
    {
        var job = MechanicRecalcJob.Enqueue(Guid.NewGuid());
        job.RequestCancellation();

        _jobRepoMock
            .Setup(r => r.GetByIdAsync(job.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        var dto = await _handler.Handle(
            new GetRecalcJobStatusQuery(job.Id),
            TestContext.Current.CancellationToken);

        dto.CancellationRequested.Should().BeTrue();
        dto.Status.Should().Be(RecalcJobStatus.Pending,
            "cancellation is a flag — it does not transition status");
    }

    /// <summary>
    /// Builds a Running job whose <see cref="MechanicRecalcJob.StartedAt"/> sits at
    /// (fake-now − <paramref name="startedSecondsAgo"/>) using <see cref="MechanicRecalcJob.Reconstitute"/>.
    /// </summary>
    /// <remarks>
    /// The aggregate's lifecycle methods (<see cref="MechanicRecalcJob.MarkRunning(int)"/>,
    /// <see cref="MechanicRecalcJob.RecordSuccess"/>) stamp timestamps via
    /// <c>DateTimeOffset.UtcNow</c>, which would not align with the handler's
    /// <see cref="TimeProvider"/>-based "now". Reconstitute bypasses the lifecycle and lets us
    /// pin every field deterministically.
    /// </remarks>
    private MechanicRecalcJob BuildRunningJob(int processed, int total, double startedSecondsAgo)
    {
        var now = _timeProvider.GetUtcNow();
        var startedAt = now.AddSeconds(-startedSecondsAgo);

        return MechanicRecalcJob.Reconstitute(
            id: Guid.NewGuid(),
            status: RecalcJobStatus.Running,
            triggeredByUserId: Guid.NewGuid(),
            total: total,
            processed: processed,
            failed: 0,
            skipped: 0,
            consecutiveFailures: 0,
            lastError: null,
            lastProcessedAnalysisId: null,
            cancellationRequested: false,
            createdAt: startedAt,
            startedAt: startedAt,
            completedAt: null,
            heartbeatAt: startedAt);
    }
}
