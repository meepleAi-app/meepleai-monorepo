using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// State-machine unit tests for <see cref="KbReindexJob"/>.
/// Issue #941 / ADR-057.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class KbReindexJobTests
{
    private static readonly Guid GameId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void Create_ValidArguments_ProducesQueuedJob()
    {
        var job = KbReindexJob.Create(GameId, UserId, totalPdfs: 5);

        job.Id.Should().NotBe(Guid.Empty);
        job.GameId.Should().Be(GameId);
        job.UserId.Should().Be(UserId);
        job.TotalPdfs.Should().Be(5);
        job.ProcessedPdfs.Should().Be(0);
        job.Status.Should().Be(KbReindexJobStatus.Queued);
        job.StartedAt.Should().BeNull();
        job.CompletedAt.Should().BeNull();
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void Create_EmptyGameId_Throws(string id)
    {
        var act = () => KbReindexJob.Create(Guid.Parse(id), UserId, 1);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_NegativeTotal_Throws()
    {
        var act = () => KbReindexJob.Create(GameId, UserId, totalPdfs: -1);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void StateMachine_QueuedToRunningToCompleted_HappyPath()
    {
        var job = KbReindexJob.Create(GameId, UserId, totalPdfs: 2);

        job.MarkRunning();
        job.Status.Should().Be(KbReindexJobStatus.Running);
        job.StartedAt.Should().NotBeNull();

        job.IncrementProcessed();
        job.IncrementProcessed();
        job.ProcessedPdfs.Should().Be(2);

        job.MarkCompleted();
        job.Status.Should().Be(KbReindexJobStatus.Completed);
        job.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void StateMachine_QueuedToCompleted_ZeroPdfShortCircuit()
    {
        var job = KbReindexJob.Create(GameId, UserId, totalPdfs: 0);
        job.MarkCompleted();
        job.Status.Should().Be(KbReindexJobStatus.Completed);
    }

    [Fact]
    public void StateMachine_CompletedCannotRevertToRunning()
    {
        var job = KbReindexJob.Create(GameId, UserId, 1);
        job.MarkRunning();
        job.MarkCompleted();

        var act = () => job.MarkRunning();
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*completed*running*");
    }

    [Fact]
    public void StateMachine_RunningToFailedWithReason()
    {
        var job = KbReindexJob.Create(GameId, UserId, 1);
        job.MarkRunning();
        job.MarkFailed("downstream embedding service unavailable");

        job.Status.Should().Be(KbReindexJobStatus.Failed);
        job.FailureReason.Should().Contain("embedding service");
        job.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void StateMachine_FailedCannotTransitionToCompleted()
    {
        var job = KbReindexJob.Create(GameId, UserId, 1);
        job.MarkRunning();
        job.MarkFailed("error");

        var act = () => job.MarkCompleted();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkFailed_RequiresReason()
    {
        var job = KbReindexJob.Create(GameId, UserId, 1);
        job.MarkRunning();

        var act = () => job.MarkFailed(reason: "");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void IncrementProcessed_PastTotal_Throws()
    {
        var job = KbReindexJob.Create(GameId, UserId, totalPdfs: 1);
        job.MarkRunning();
        job.IncrementProcessed();

        var act = () => job.IncrementProcessed();
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*cannot exceed*");
    }

    [Fact]
    public void IncrementProcessed_BeforeRunning_Throws()
    {
        var job = KbReindexJob.Create(GameId, UserId, 1);

        var act = () => job.IncrementProcessed();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Hydrate_RoundTrip_PreservesAllFields()
    {
        var id = Guid.NewGuid();
        var created = DateTime.UtcNow.AddMinutes(-10);
        var started = created.AddSeconds(5);
        var completed = started.AddMinutes(8);

        var job = KbReindexJob.Hydrate(
            id, GameId, UserId,
            status: KbReindexJobStatus.Completed,
            totalPdfs: 7, processedPdfs: 7,
            createdAt: created, startedAt: started, completedAt: completed,
            failureReason: null);

        job.Id.Should().Be(id);
        job.GameId.Should().Be(GameId);
        job.UserId.Should().Be(UserId);
        job.Status.Should().Be(KbReindexJobStatus.Completed);
        job.TotalPdfs.Should().Be(7);
        job.ProcessedPdfs.Should().Be(7);
        job.CreatedAt.Should().Be(created);
        job.StartedAt.Should().Be(started);
        job.CompletedAt.Should().Be(completed);
        job.FailureReason.Should().BeNull();
    }
}
