using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.Aggregates;

/// <summary>
/// Unit tests for the <see cref="MechanicRecalcJob"/> aggregate root (ADR-051 M2.1, Sprint 2, Task 6).
/// Covers the job lifecycle state machine and counter invariants.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class MechanicRecalcJobTests
{
    // ============================================================
    // 1. Enqueue → Pending, no timestamps, no counters
    // ============================================================

    [Fact]
    public void Enqueue_WithValidUserId_CreatesPendingJobWithDefaults()
    {
        var userId = Guid.NewGuid();

        var job = MechanicRecalcJob.Enqueue(userId);

        job.Id.Should().NotBe(Guid.Empty);
        job.Status.Should().Be(RecalcJobStatus.Pending);
        job.TriggeredByUserId.Should().Be(userId);
        job.Total.Should().Be(0);
        job.Processed.Should().Be(0);
        job.Failed.Should().Be(0);
        job.Skipped.Should().Be(0);
        job.ConsecutiveFailures.Should().Be(0);
        job.CancellationRequested.Should().BeFalse();
        job.StartedAt.Should().BeNull();
        job.CompletedAt.Should().BeNull();
        job.HeartbeatAt.Should().BeNull();
        job.LastError.Should().BeNull();
        job.LastProcessedAnalysisId.Should().BeNull();
    }

    [Fact]
    public void Enqueue_WithEmptyUserId_Throws()
    {
        var act = () => MechanicRecalcJob.Enqueue(Guid.Empty);
        act.Should().Throw<ArgumentException>().WithMessage("*TriggeredByUserId*");
    }

    // ============================================================
    // 2. MarkRunning: Pending → Running, sets Total + StartedAt
    // ============================================================

    [Fact]
    public void MarkRunning_FromPending_TransitionsToRunning_AndSetsStartedAt()
    {
        var job = MechanicRecalcJob.Enqueue(Guid.NewGuid());

        job.MarkRunning(total: 42);

        job.Status.Should().Be(RecalcJobStatus.Running);
        job.Total.Should().Be(42);
        job.StartedAt.Should().NotBeNull();
        job.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void MarkRunning_WhenAlreadyRunning_Throws()
    {
        var job = RunningJob();

        var act = () => job.MarkRunning(total: 5);

        act.Should().Throw<InvalidMechanicRecalcJobTransitionException>()
            .Which.CurrentStatus.Should().Be(RecalcJobStatus.Running);
    }

    // ============================================================
    // 3. RecordSuccess: increments Processed, resets ConsecutiveFailures
    // ============================================================

    [Fact]
    public void RecordSuccess_IncrementsProcessed_ResetsConsecutiveFailures_SetsLastAnalysisId()
    {
        var job = RunningJob();
        var analysisId = Guid.NewGuid();

        // Pre-condition: simulate a prior failure to verify reset
        job.RecordFailure(Guid.NewGuid(), "previous error");
        job.ConsecutiveFailures.Should().Be(1);

        job.RecordSuccess(analysisId);

        job.Processed.Should().Be(1);
        job.ConsecutiveFailures.Should().Be(0);
        job.LastProcessedAnalysisId.Should().Be(analysisId);
        job.Failed.Should().Be(1); // failure counter is not rolled back
    }

    // ============================================================
    // 4. RecordFailure: increments Failed + ConsecutiveFailures; ≥5 stays Running
    // ============================================================

    [Fact]
    public void RecordFailure_IncrementsFailedAndConsecutiveFailures_SetsLastError()
    {
        var job = RunningJob();
        var analysisId = Guid.NewGuid();

        job.RecordFailure(analysisId, "timeout");

        job.Failed.Should().Be(1);
        job.ConsecutiveFailures.Should().Be(1);
        job.LastError.Should().Be("timeout");
        job.LastProcessedAnalysisId.Should().Be(analysisId);
    }

    [Fact]
    public void RecordFailure_FiveConsecutiveTimes_StaysRunning_DoesNotAutoFail()
    {
        var job = RunningJob();

        for (var i = 0; i < 5; i++)
        {
            job.RecordFailure(Guid.NewGuid(), $"error {i}");
        }

        job.ConsecutiveFailures.Should().Be(5);
        job.Status.Should().Be(RecalcJobStatus.Running); // worker decides, not the aggregate
    }

    // ============================================================
    // 5. RecordSkip: increments Skipped, does NOT touch ConsecutiveFailures
    // ============================================================

    [Fact]
    public void RecordSkip_IncrementsSkipped_LeaveConsecutiveFailuresUnchanged()
    {
        var job = RunningJob();
        job.RecordFailure(Guid.NewGuid(), "err");

        job.RecordSkip();

        job.Skipped.Should().Be(1);
        job.ConsecutiveFailures.Should().Be(1); // unchanged
    }

    // ============================================================
    // 6. RequestCancellation: sets flag from Pending or Running; idempotent
    // ============================================================

    [Fact]
    public void RequestCancellation_FromPending_SetsFlagWithoutChangingStatus()
    {
        var job = MechanicRecalcJob.Enqueue(Guid.NewGuid());

        job.RequestCancellation();

        job.CancellationRequested.Should().BeTrue();
        job.Status.Should().Be(RecalcJobStatus.Pending);
    }

    [Fact]
    public void RequestCancellation_TwiceFromRunning_IsIdempotent()
    {
        var job = RunningJob();

        job.RequestCancellation();
        var act = () => job.RequestCancellation();

        act.Should().NotThrow();
        job.CancellationRequested.Should().BeTrue();
    }

    [Fact]
    public void RequestCancellation_FromCompleted_Throws()
    {
        var job = RunningJob();
        job.Complete();

        var act = () => job.RequestCancellation();

        act.Should().Throw<InvalidMechanicRecalcJobTransitionException>()
            .Which.CurrentStatus.Should().Be(RecalcJobStatus.Completed);
    }

    // ============================================================
    // 7. Complete: Running → Completed, sets CompletedAt
    // ============================================================

    [Fact]
    public void Complete_FromRunning_TransitionsToCompleted_AndSetsCompletedAt()
    {
        var job = RunningJob();

        job.Complete();

        job.Status.Should().Be(RecalcJobStatus.Completed);
        job.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void Complete_FromPending_Throws()
    {
        var job = MechanicRecalcJob.Enqueue(Guid.NewGuid());

        var act = () => job.Complete();

        act.Should().Throw<InvalidMechanicRecalcJobTransitionException>()
            .Which.CurrentStatus.Should().Be(RecalcJobStatus.Pending);
    }

    // ============================================================
    // 8. Fail: Pending/Running → Failed, captures reason + CompletedAt
    // ============================================================

    [Fact]
    public void Fail_FromRunning_TransitionsToFailed_WithReason()
    {
        var job = RunningJob();

        job.Fail("circuit breaker tripped");

        job.Status.Should().Be(RecalcJobStatus.Failed);
        job.LastError.Should().Be("circuit breaker tripped");
        job.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void Fail_FromPending_TransitionsToFailed()
    {
        var job = MechanicRecalcJob.Enqueue(Guid.NewGuid());

        job.Fail("failed before pickup");

        job.Status.Should().Be(RecalcJobStatus.Failed);
        job.LastError.Should().Be("failed before pickup");
        job.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void Fail_FromCompleted_Throws()
    {
        var job = RunningJob();
        job.Complete();

        var act = () => job.Fail("late failure");

        act.Should().Throw<InvalidMechanicRecalcJobTransitionException>()
            .Which.CurrentStatus.Should().Be(RecalcJobStatus.Completed);
    }

    // ============================================================
    // 9. Heartbeat: updates HeartbeatAt only when Running
    // ============================================================

    [Fact]
    public void Heartbeat_WhenRunning_UpdatesHeartbeatAt()
    {
        var job = RunningJob();

        job.Heartbeat();

        job.HeartbeatAt.Should().NotBeNull();
        job.Status.Should().Be(RecalcJobStatus.Running); // unchanged
    }

    [Fact]
    public void Heartbeat_WhenNotRunning_Throws()
    {
        var job = MechanicRecalcJob.Enqueue(Guid.NewGuid());

        var act = () => job.Heartbeat();

        act.Should().Throw<InvalidMechanicRecalcJobTransitionException>()
            .Which.CurrentStatus.Should().Be(RecalcJobStatus.Pending);
    }

    // ============================================================
    // Helpers
    // ============================================================

    private static MechanicRecalcJob RunningJob()
    {
        var job = MechanicRecalcJob.Enqueue(Guid.NewGuid());
        job.MarkRunning(total: 10);
        return job;
    }
}
