using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Enums;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.Administration.Domain.Entities;

/// <summary>
/// Unit tests for BatchJob aggregate (Issue #3693 - Task 4)
/// </summary>
public sealed class BatchJobTests
{
    [Fact]
    public void Create_ValidParameters_ReturnsBatchJob()
    {
        // Arrange
        var type = JobType.ResourceForecast;
        var parameters = "{\"days\": 30}";
        var createdBy = Guid.NewGuid();

        // Act
        var job = BatchJob.Create(type, parameters, createdBy);

        // Assert
        job.Id.Should().NotBe(Guid.Empty);
        job.Type.Should().Be(type);
        job.Status.Should().Be(JobStatus.Queued);
        job.Parameters.Should().Be(parameters);
        job.Progress.Should().Be(0);
        job.CreatedBy.Should().Be(createdBy);
    }

    [Fact]
    public void Start_QueuedJob_TransitionsToRunning()
    {
        // Arrange
        var job = BatchJob.Create(JobType.CostAnalysis, "{}", Guid.NewGuid());

        // Act
        job.Start();

        // Assert
        job.Status.Should().Be(JobStatus.Running);
        job.StartedAt.Should().NotBeNull();
        job.Progress.Should().Be(0);
    }

    [Fact]
    public void Complete_RunningJob_SetsCompletedStatus()
    {
        // Arrange
        var job = BatchJob.Create(JobType.DataCleanup, "{}", Guid.NewGuid());
        job.Start();

        // Act
        job.Complete(resultData: "{\"cleaned\": 100}", resultSummary: "Done", outputFileUrl: null);

        // Assert
        job.Status.Should().Be(JobStatus.Completed);
        job.Progress.Should().Be(100);
        job.CompletedAt.Should().NotBeNull();
        job.ResultSummary.Should().Be("Done");
    }

    [Fact]
    public void Fail_RunningJob_SetsFailedStatusAndIncrementsRetryCount()
    {
        // Arrange
        var job = BatchJob.Create(JobType.BggSync, "{}", Guid.NewGuid());
        job.Start();

        // Act
        job.Fail("Connection timeout", "stack trace");

        // Assert
        job.Status.Should().Be(JobStatus.Failed);
        job.ErrorMessage.Should().Be("Connection timeout");
        job.RetryCount.Should().Be(1);
        job.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void Retry_FailedJob_ResetsToQueued()
    {
        // Arrange
        var job = BatchJob.Create(JobType.AgentBenchmark, "{}", Guid.NewGuid());
        job.Start();
        job.Fail("Error");

        // Act
        job.Retry();

        // Assert
        job.Status.Should().Be(JobStatus.Queued);
        job.Progress.Should().Be(0);
        job.StartedAt.Should().BeNull();
        job.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Cancel_QueuedJob_SetsCancelledStatus()
    {
        // Arrange
        var job = BatchJob.Create(JobType.ResourceForecast, "{}", Guid.NewGuid());

        // Act
        job.Cancel();

        // Assert
        job.Status.Should().Be(JobStatus.Cancelled);
        job.CompletedAt.Should().NotBeNull();
    }
}