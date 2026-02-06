using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Enums;

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
        Assert.NotEqual(Guid.Empty, job.Id);
        Assert.Equal(type, job.Type);
        Assert.Equal(JobStatus.Queued, job.Status);
        Assert.Equal(parameters, job.Parameters);
        Assert.Equal(0, job.Progress);
        Assert.Equal(createdBy, job.CreatedBy);
    }

    [Fact]
    public void Start_QueuedJob_TransitionsToRunning()
    {
        // Arrange
        var job = BatchJob.Create(JobType.CostAnalysis, "{}", Guid.NewGuid());

        // Act
        job.Start();

        // Assert
        Assert.Equal(JobStatus.Running, job.Status);
        Assert.NotNull(job.StartedAt);
        Assert.Equal(0, job.Progress);
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
        Assert.Equal(JobStatus.Completed, job.Status);
        Assert.Equal(100, job.Progress);
        Assert.NotNull(job.CompletedAt);
        Assert.Equal("Done", job.ResultSummary);
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
        Assert.Equal(JobStatus.Failed, job.Status);
        Assert.Equal("Connection timeout", job.ErrorMessage);
        Assert.Equal(1, job.RetryCount);
        Assert.NotNull(job.CompletedAt);
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
        Assert.Equal(JobStatus.Queued, job.Status);
        Assert.Equal(0, job.Progress);
        Assert.Null(job.StartedAt);
        Assert.Null(job.ErrorMessage);
    }

    [Fact]
    public void Cancel_QueuedJob_SetsCancelledStatus()
    {
        // Arrange
        var job = BatchJob.Create(JobType.ResourceForecast, "{}", Guid.NewGuid());

        // Act
        job.Cancel();

        // Assert
        Assert.Equal(JobStatus.Cancelled, job.Status);
        Assert.NotNull(job.CompletedAt);
    }
}
