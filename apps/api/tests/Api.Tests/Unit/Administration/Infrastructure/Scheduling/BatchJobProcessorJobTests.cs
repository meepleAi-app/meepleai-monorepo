using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Enums;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Administration.Infrastructure.Scheduling;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Quartz;
using Xunit;

namespace Api.Tests.Unit.Administration.Infrastructure.Scheduling;

/// <summary>
/// Unit tests for BatchJobProcessorJob background worker (Issue #3693)
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
public sealed class BatchJobProcessorJobTests
{
    private readonly Mock<IBatchJobRepository> _repositoryMock;
    private readonly Mock<ILogger<BatchJobProcessorJob>> _loggerMock;
    private readonly Mock<IJobExecutionContext> _contextMock;
    private readonly BatchJobProcessorJob _processor;
    private static readonly Guid TestUserId = Guid.NewGuid();

    public BatchJobProcessorJobTests()
    {
        _repositoryMock = new Mock<IBatchJobRepository>();
        _loggerMock = new Mock<ILogger<BatchJobProcessorJob>>();
        _contextMock = new Mock<IJobExecutionContext>();

        _contextMock.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

        _processor = new BatchJobProcessorJob(
            _repositoryMock.Object,
            _loggerMock.Object
        );
    }

    #region Execute Tests

    [Fact]
    public async Task Execute_WhenNoQueuedJobs_ShouldDoNothing()
    {
        // Arrange
        _repositoryMock
            .Setup(x => x.GetByStatusAsync(JobStatus.Queued, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BatchJob>());

        // Act
        await _processor.Execute(_contextMock.Object);

        // Assert
        _repositoryMock.Verify(
            x => x.UpdateAsync(It.IsAny<BatchJob>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Execute_WithQueuedJob_ShouldStartJob()
    {
        // Arrange
        var job = BatchJob.Create(JobType.ResourceForecast, "{}", TestUserId);
        _repositoryMock
            .Setup(x => x.GetByStatusAsync(JobStatus.Queued, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BatchJob> { job });

        // Act
        await _processor.Execute(_contextMock.Object);

        // Assert
        job.Status.Should().Be(JobStatus.Completed);
        job.StartedAt.Should().NotBeNull();
        job.Progress.Should().Be(100);

        _repositoryMock.Verify(
            x => x.UpdateAsync(It.Is<BatchJob>(j => j.Id == job.Id), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce
        );
    }

    [Fact]
    public async Task Execute_WithMultipleQueuedJobs_ShouldProcessOnlyFirst()
    {
        // Arrange
        var job1 = BatchJob.Create(JobType.ResourceForecast, "{}", TestUserId);
        var job2 = BatchJob.Create(JobType.CostAnalysis, "{}", TestUserId);

        _repositoryMock
            .Setup(x => x.GetByStatusAsync(JobStatus.Queued, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BatchJob> { job1, job2 });

        // Act
        await _processor.Execute(_contextMock.Object);

        // Assert
        job1.Status.Should().Be(JobStatus.Completed);
        job2.Status.Should().Be(JobStatus.Queued); // Should remain queued

        _repositoryMock.Verify(
            x => x.UpdateAsync(It.Is<BatchJob>(j => j.Id == job1.Id), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce
        );

        _repositoryMock.Verify(
            x => x.UpdateAsync(It.Is<BatchJob>(j => j.Id == job2.Id), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Execute_WithQueuedJob_ShouldUpdateProgress()
    {
        // Arrange
        var job = BatchJob.Create(JobType.DataCleanup, "{}", TestUserId);
        var progressUpdates = new List<int>();

        _repositoryMock
            .Setup(x => x.GetByStatusAsync(JobStatus.Queued, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BatchJob> { job });

        _repositoryMock
            .Setup(x => x.UpdateAsync(It.IsAny<BatchJob>(), It.IsAny<CancellationToken>()))
            .Callback<BatchJob, CancellationToken>((j, _) => progressUpdates.Add(j.Progress))
            .Returns(Task.CompletedTask);

        // Act
        await _processor.Execute(_contextMock.Object);

        // Assert
        progressUpdates.Should().Contain(new[] { 0, 20, 40, 60, 80, 100 });
    }

    [Fact]
    public async Task Execute_WhenJobCompletes_ShouldMarkAsCompleted()
    {
        // Arrange
        var job = BatchJob.Create(JobType.BggSync, "{}", TestUserId);
        _repositoryMock
            .Setup(x => x.GetByStatusAsync(JobStatus.Queued, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BatchJob> { job });

        // Act
        await _processor.Execute(_contextMock.Object);

        // Assert
        job.Status.Should().Be(JobStatus.Completed);
        job.CompletedAt.Should().NotBeNull();
        job.Progress.Should().Be(100);
        job.DurationSeconds.Should().BeGreaterThanOrEqualTo(0);
        job.ResultData.Should().NotBeNullOrEmpty();
        job.ResultSummary.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Execute_ProcessesResourceForecastJob()
    {
        // Arrange
        var job = BatchJob.Create(JobType.ResourceForecast, "{}", TestUserId);
        _repositoryMock
            .Setup(x => x.GetByStatusAsync(JobStatus.Queued, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BatchJob> { job });

        // Act
        await _processor.Execute(_contextMock.Object);

        // Assert
        job.Type.Should().Be(JobType.ResourceForecast);
        job.Status.Should().Be(JobStatus.Completed);
    }

    [Fact]
    public async Task Execute_ProcessesCostAnalysisJob()
    {
        // Arrange
        var job = BatchJob.Create(JobType.CostAnalysis, "{}", TestUserId);
        _repositoryMock
            .Setup(x => x.GetByStatusAsync(JobStatus.Queued, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BatchJob> { job });

        // Act
        await _processor.Execute(_contextMock.Object);

        // Assert
        job.Type.Should().Be(JobType.CostAnalysis);
        job.Status.Should().Be(JobStatus.Completed);
    }

    [Fact]
    public async Task Execute_ProcessesDataCleanupJob()
    {
        // Arrange
        var job = BatchJob.Create(JobType.DataCleanup, "{}", TestUserId);
        _repositoryMock
            .Setup(x => x.GetByStatusAsync(JobStatus.Queued, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BatchJob> { job });

        // Act
        await _processor.Execute(_contextMock.Object);

        // Assert
        job.Type.Should().Be(JobType.DataCleanup);
        job.Status.Should().Be(JobStatus.Completed);
    }

    [Fact]
    public async Task Execute_ProcessesBggSyncJob()
    {
        // Arrange
        var job = BatchJob.Create(JobType.BggSync, "{}", TestUserId);
        _repositoryMock
            .Setup(x => x.GetByStatusAsync(JobStatus.Queued, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BatchJob> { job });

        // Act
        await _processor.Execute(_contextMock.Object);

        // Assert
        job.Type.Should().Be(JobType.BggSync);
        job.Status.Should().Be(JobStatus.Completed);
    }

    #endregion

    #region FIFO Processing Tests

    [Fact]
    public async Task Execute_WithMultipleQueuedJobs_ShouldProcessOldestFirst()
    {
        // Arrange
        var olderJob = BatchJob.Create(JobType.ResourceForecast, "{}", TestUserId);
        await Task.Delay(50);
        var newerJob = BatchJob.Create(JobType.CostAnalysis, "{}", TestUserId);

        _repositoryMock
            .Setup(x => x.GetByStatusAsync(JobStatus.Queued, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BatchJob> { newerJob, olderJob }); // Unordered

        var processedJobId = Guid.Empty;
        _repositoryMock
            .Setup(x => x.UpdateAsync(It.IsAny<BatchJob>(), It.IsAny<CancellationToken>()))
            .Callback<BatchJob, CancellationToken>((j, _) =>
            {
                if (j.Status == JobStatus.Running && processedJobId == Guid.Empty)
                {
                    processedJobId = j.Id;
                }
            })
            .Returns(Task.CompletedTask);

        // Act
        await _processor.Execute(_contextMock.Object);

        // Assert
        processedJobId.Should().Be(olderJob.Id);
    }

    #endregion

    #region Logger Tests

    [Fact]
    public async Task Execute_WhenProcessingJob_ShouldLogStartAndCompletion()
    {
        // Arrange
        var job = BatchJob.Create(JobType.ResourceForecast, "{}", TestUserId);
        _repositoryMock
            .Setup(x => x.GetByStatusAsync(JobStatus.Queued, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BatchJob> { job });

        // Act
        await _processor.Execute(_contextMock.Object);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Processing batch job")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once
        );

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("completed")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once
        );
    }

    #endregion
}
