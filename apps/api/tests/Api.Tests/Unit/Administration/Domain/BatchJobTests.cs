using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Unit.Administration.Domain;

/// <summary>
/// Unit tests for BatchJob entity domain logic (Issue #3693)
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
public sealed class BatchJobTests
{
    private static readonly Guid TestUserId = Guid.NewGuid();

    #region Create Tests

    [Fact]
    public void Create_WithValidParameters_ShouldCreateBatchJob()
    {
        // Arrange
        var jobType = JobType.ResourceForecast;
        var parameters = "{\"forecast_days\":30}";

        // Act
        var job = BatchJob.Create(jobType, parameters, TestUserId);

        // Assert
        job.Should().NotBeNull();
        job.Id.Should().NotBeEmpty();
        job.Type.Should().Be(jobType);
        job.Status.Should().Be(JobStatus.Queued);
        job.Parameters.Should().Be(parameters);
        job.Progress.Should().Be(0);
        job.RetryCount.Should().Be(0);
        job.CreatedBy.Should().Be(TestUserId);
        job.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TestConstants.Timing.AssertionTolerance);
    }

    [Fact]
    public void Create_WithEmptyParameters_ShouldThrowArgumentException()
    {
        // Arrange
        var jobType = JobType.CostAnalysis;

        // Act
        var act = () => BatchJob.Create(jobType, "", TestUserId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("parameters");
    }

    [Fact]
    public void Create_WithNullParameters_ShouldThrowArgumentException()
    {
        // Arrange
        var jobType = JobType.DataCleanup;

        // Act
        var act = () => BatchJob.Create(jobType, null!, TestUserId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("parameters");
    }

    [Fact]
    public void Create_WithEmptyUserId_ShouldThrowArgumentException()
    {
        // Arrange
        var jobType = JobType.BggSync;
        var parameters = "{\"limit\":1000}";

        // Act
        var act = () => BatchJob.Create(jobType, parameters, Guid.Empty);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("createdBy");
    }

    #endregion

    #region Start Tests

    [Fact]
    public void Start_WhenQueued_ShouldUpdateStatusToRunning()
    {
        // Arrange
        var job = BatchJob.Create(JobType.ResourceForecast, "{}", TestUserId);

        // Act
        job.Start();

        // Assert
        job.Status.Should().Be(JobStatus.Running);
        job.StartedAt.Should().BeCloseTo(DateTime.UtcNow, TestConstants.Timing.AssertionTolerance);
        job.Progress.Should().Be(0);
    }

    [Fact]
    public void Start_WhenAlreadyRunning_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var job = BatchJob.Create(JobType.CostAnalysis, "{}", TestUserId);
        job.Start();

        // Act
        var act = () => job.Start();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Only queued jobs can be started");
    }

    [Fact]
    public void Start_WhenCompleted_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var job = BatchJob.Create(JobType.DataCleanup, "{}", TestUserId);
        job.Start();
        job.Complete(null, "Success", null);

        // Act
        var act = () => job.Start();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Only queued jobs can be started");
    }

    #endregion

    #region UpdateProgress Tests

    [Fact]
    public void UpdateProgress_WithValidProgress_ShouldUpdateValue()
    {
        // Arrange
        var job = BatchJob.Create(JobType.BggSync, "{}", TestUserId);
        job.Start();

        // Act
        job.UpdateProgress(50);

        // Assert
        job.Progress.Should().Be(50);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    [InlineData(50)]
    [InlineData(75)]
    [InlineData(100)]
    public void UpdateProgress_WithBoundaryValues_ShouldSucceed(int progress)
    {
        // Arrange
        var job = BatchJob.Create(JobType.ResourceForecast, "{}", TestUserId);
        job.Start();

        // Act
        job.UpdateProgress(progress);

        // Assert
        job.Progress.Should().Be(progress);
    }

    [Fact]
    public void UpdateProgress_WithNegativeValue_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var job = BatchJob.Create(JobType.CostAnalysis, "{}", TestUserId);
        job.Start();

        // Act
        var act = () => job.UpdateProgress(-1);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("progress");
    }

    [Fact]
    public void UpdateProgress_WithValueAbove100_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var job = BatchJob.Create(JobType.DataCleanup, "{}", TestUserId);
        job.Start();

        // Act
        var act = () => job.UpdateProgress(101);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("progress");
    }

    #endregion

    #region Complete Tests

    [Fact]
    public void Complete_WithValidData_ShouldMarkAsCompleted()
    {
        // Arrange
        var job = BatchJob.Create(JobType.ResourceForecast, "{}", TestUserId);
        job.Start();
        var resultData = "{\"forecast\":[{\"date\":\"2025-02-01\",\"value\":1000}]}";
        var resultSummary = "Forecast generated for 30 days";
        var outputUrl = "https://storage.example.com/forecast.csv";

        // Act
        job.Complete(resultData, resultSummary, outputUrl);

        // Assert
        job.Status.Should().Be(JobStatus.Completed);
        job.Progress.Should().Be(100);
        job.CompletedAt.Should().BeCloseTo(DateTime.UtcNow, TestConstants.Timing.AssertionTolerance);
        job.ResultData.Should().Be(resultData);
        job.ResultSummary.Should().Be(resultSummary);
        job.OutputFileUrl.Should().Be(outputUrl);
        job.DurationSeconds.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void Complete_WithNullOptionalParameters_ShouldSucceed()
    {
        // Arrange
        var job = BatchJob.Create(JobType.DataCleanup, "{}", TestUserId);
        job.Start();

        // Act
        job.Complete(null, null, null);

        // Assert
        job.Status.Should().Be(JobStatus.Completed);
        job.Progress.Should().Be(100);
        job.ResultData.Should().BeNull();
        job.ResultSummary.Should().BeNull();
        job.OutputFileUrl.Should().BeNull();
    }

    [Fact]
    public async Task Complete_ShouldCalculateDuration()
    {
        // Arrange
        var job = BatchJob.Create(JobType.BggSync, "{}", TestUserId);
        job.Start();
        await Task.Delay(1100); // Simulate execution time (need >1s for integer duration)

        // Act
        job.Complete(null, "Synced 100 games", null);

        // Assert
        job.DurationSeconds.Should().BeGreaterThan(0);
    }

    #endregion

    #region Fail Tests

    [Fact]
    public void Fail_WithErrorMessage_ShouldMarkAsFailed()
    {
        // Arrange
        var job = BatchJob.Create(JobType.CostAnalysis, "{}", TestUserId);
        job.Start();
        var errorMessage = "Database connection timeout";
        var errorStack = "at System.Data.SqlClient.Connection.Open()";

        // Act
        job.Fail(errorMessage, errorStack);

        // Assert
        job.Status.Should().Be(JobStatus.Failed);
        job.ErrorMessage.Should().Be(errorMessage);
        job.ErrorStack.Should().Be(errorStack);
        job.RetryCount.Should().Be(1);
        job.CompletedAt.Should().BeCloseTo(DateTime.UtcNow, TestConstants.Timing.AssertionTolerance);
        job.DurationSeconds.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void Fail_WithoutErrorStack_ShouldSucceed()
    {
        // Arrange
        var job = BatchJob.Create(JobType.ResourceForecast, "{}", TestUserId);
        job.Start();
        var errorMessage = "API rate limit exceeded";

        // Act
        job.Fail(errorMessage);

        // Assert
        job.Status.Should().Be(JobStatus.Failed);
        job.ErrorMessage.Should().Be(errorMessage);
        job.ErrorStack.Should().BeNull();
        job.RetryCount.Should().Be(1);
    }

    [Fact]
    public void Fail_MultipleTimes_ShouldIncrementRetryCount()
    {
        // Arrange
        var job = BatchJob.Create(JobType.DataCleanup, "{}", TestUserId);

        // Act
        job.Start();
        job.Fail("First failure");

        job.Retry();
        job.Start();
        job.Fail("Second failure");

        job.Retry();
        job.Start();
        job.Fail("Third failure");

        // Assert
        job.RetryCount.Should().Be(3);
        job.Status.Should().Be(JobStatus.Failed);
    }

    #endregion

    #region Cancel Tests

    [Fact]
    public void Cancel_WhenQueued_ShouldMarkAsCancelled()
    {
        // Arrange
        var job = BatchJob.Create(JobType.BggSync, "{}", TestUserId);

        // Act
        job.Cancel();

        // Assert
        job.Status.Should().Be(JobStatus.Cancelled);
        job.CompletedAt.Should().BeCloseTo(DateTime.UtcNow, TestConstants.Timing.AssertionTolerance);
    }

    [Fact]
    public void Cancel_WhenRunning_ShouldMarkAsCancelled()
    {
        // Arrange
        var job = BatchJob.Create(JobType.ResourceForecast, "{}", TestUserId);
        job.Start();

        // Act
        job.Cancel();

        // Assert
        job.Status.Should().Be(JobStatus.Cancelled);
        job.CompletedAt.Should().BeCloseTo(DateTime.UtcNow, TestConstants.Timing.AssertionTolerance);
    }

    [Fact]
    public void Cancel_WhenCompleted_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var job = BatchJob.Create(JobType.CostAnalysis, "{}", TestUserId);
        job.Start();
        job.Complete(null, "Success", null);

        // Act
        var act = () => job.Cancel();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot cancel completed or failed jobs");
    }

    [Fact]
    public void Cancel_WhenFailed_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var job = BatchJob.Create(JobType.DataCleanup, "{}", TestUserId);
        job.Start();
        job.Fail("Error occurred");

        // Act
        var act = () => job.Cancel();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot cancel completed or failed jobs");
    }

    #endregion

    #region Retry Tests

    [Fact]
    public void Retry_WhenFailed_ShouldRequeueJob()
    {
        // Arrange
        var job = BatchJob.Create(JobType.BggSync, "{}", TestUserId);
        job.Start();
        job.UpdateProgress(50);
        job.Fail("Network error");

        // Act
        job.Retry();

        // Assert
        job.Status.Should().Be(JobStatus.Queued);
        job.Progress.Should().Be(0);
        job.StartedAt.Should().BeNull();
        job.CompletedAt.Should().BeNull();
        job.DurationSeconds.Should().BeNull();
        job.ErrorMessage.Should().BeNull();
        job.ErrorStack.Should().BeNull();
    }

    [Fact]
    public void Retry_WhenQueued_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var job = BatchJob.Create(JobType.ResourceForecast, "{}", TestUserId);

        // Act
        var act = () => job.Retry();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Only failed jobs can be retried");
    }

    [Fact]
    public void Retry_WhenRunning_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var job = BatchJob.Create(JobType.CostAnalysis, "{}", TestUserId);
        job.Start();

        // Act
        var act = () => job.Retry();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Only failed jobs can be retried");
    }

    [Fact]
    public void Retry_WhenCompleted_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var job = BatchJob.Create(JobType.DataCleanup, "{}", TestUserId);
        job.Start();
        job.Complete(null, "Success", null);

        // Act
        var act = () => job.Retry();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Only failed jobs can be retried");
    }

    [Fact]
    public void Retry_WhenCancelled_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var job = BatchJob.Create(JobType.BggSync, "{}", TestUserId);
        job.Cancel();

        // Act
        var act = () => job.Retry();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Only failed jobs can be retried");
    }

    #endregion

    #region JobType Tests

    [Theory]
    [InlineData(JobType.ResourceForecast)]
    [InlineData(JobType.CostAnalysis)]
    [InlineData(JobType.DataCleanup)]
    [InlineData(JobType.BggSync)]
    [InlineData(JobType.AgentBenchmark)]
    public void Create_WithAllJobTypes_ShouldSucceed(JobType jobType)
    {
        // Act
        var job = BatchJob.Create(jobType, "{}", TestUserId);

        // Assert
        job.Type.Should().Be(jobType);
        job.Status.Should().Be(JobStatus.Queued);
    }

    #endregion
}
