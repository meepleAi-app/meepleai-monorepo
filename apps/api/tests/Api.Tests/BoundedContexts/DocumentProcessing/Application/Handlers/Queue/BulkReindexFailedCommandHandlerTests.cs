using Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Handlers.Queue;

[Trait("Category", TestCategories.Unit)]
public sealed class BulkReindexFailedCommandHandlerTests
{
    private readonly Mock<IProcessingJobRepository> _jobRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly BulkReindexFailedCommandHandler _handler;

    public BulkReindexFailedCommandHandlerTests()
    {
        _handler = new BulkReindexFailedCommandHandler(
            _jobRepoMock.Object,
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_NoFailedJobs_ReturnsZeroCounts()
    {
        _jobRepoMock
            .Setup(r => r.GetAllByStatusAsync(JobStatus.Failed, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProcessingJob>());

        var result = await _handler.Handle(
            new BulkReindexFailedCommand(Guid.NewGuid()), CancellationToken.None);

        result.EnqueuedCount.Should().Be(0);
        result.SkippedCount.Should().Be(0);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_FailedJobsWithRetries_RequeuesAsLowPriority()
    {
        var job = CreateFailedJob();
        _jobRepoMock
            .Setup(r => r.GetAllByStatusAsync(JobStatus.Failed, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProcessingJob> { job });
        _jobRepoMock
            .Setup(r => r.ExistsByPdfDocumentIdAsync(job.PdfDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _handler.Handle(
            new BulkReindexFailedCommand(Guid.NewGuid()), CancellationToken.None);

        result.EnqueuedCount.Should().Be(1);
        result.SkippedCount.Should().Be(0);
        _jobRepoMock.Verify(
            r => r.UpdateAsync(It.Is<ProcessingJob>(j =>
                j.Status == JobStatus.Queued &&
                j.Priority == (int)ProcessingPriority.Low),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_JobExceedsMaxRetries_SkipsWithError()
    {
        var job = CreateFailedJob(retryCount: 3, maxRetries: 3);
        _jobRepoMock
            .Setup(r => r.GetAllByStatusAsync(JobStatus.Failed, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProcessingJob> { job });

        var result = await _handler.Handle(
            new BulkReindexFailedCommand(Guid.NewGuid()), CancellationToken.None);

        result.EnqueuedCount.Should().Be(0);
        result.SkippedCount.Should().Be(1);
        result.Errors.Should().ContainSingle(e => e.Reason == "Max retries exceeded");
    }

    [Fact]
    public async Task Handle_ActiveJobExists_SkipsWithError()
    {
        var job = CreateFailedJob();
        _jobRepoMock
            .Setup(r => r.GetAllByStatusAsync(JobStatus.Failed, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProcessingJob> { job });
        _jobRepoMock
            .Setup(r => r.ExistsByPdfDocumentIdAsync(job.PdfDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _handler.Handle(
            new BulkReindexFailedCommand(Guid.NewGuid()), CancellationToken.None);

        result.EnqueuedCount.Should().Be(0);
        result.SkippedCount.Should().Be(1);
        result.Errors.Should().ContainSingle(e => e.Reason == "Active job already exists for this PDF");
    }

    [Fact]
    public async Task Handle_QueueFullViaProcessingJobs_CountsProcessingAndSkipsAll()
    {
        // Queue is full via in-flight Processing jobs, with 0 Queued. This proves Processing is
        // counted toward the capacity guard: the real cap enforced by ProcessingJob.Create is
        // (Queued + Processing < MaxQueueSize), so if this handler subtracted only Queued from
        // MaxQueueSize, availableSlots would be the full MaxQueueSize and every retryable failed
        // job would be re-enqueued past the real cap. Mirrors BulkReindexReadyCommandHandler's
        // Handle_QueueFullViaProcessingJobs_CountsProcessingAndSkipsAll.
        _jobRepoMock
            .Setup(r => r.CountByStatusAsync(JobStatus.Queued, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _jobRepoMock
            .Setup(r => r.CountByStatusAsync(JobStatus.Processing, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProcessingJob.MaxQueueSize);

        var jobs = new List<ProcessingJob>();
        for (var i = 0; i < 3; i++)
        {
            var job = CreateFailedJob();
            _jobRepoMock
                .Setup(r => r.ExistsByPdfDocumentIdAsync(job.PdfDocumentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            jobs.Add(job);
        }
        _jobRepoMock
            .Setup(r => r.GetAllByStatusAsync(JobStatus.Failed, It.IsAny<CancellationToken>()))
            .ReturnsAsync(jobs);

        var result = await _handler.Handle(
            new BulkReindexFailedCommand(Guid.NewGuid()), CancellationToken.None);

        result.EnqueuedCount.Should().Be(0);
        result.SkippedCount.Should().Be(3);
        result.Errors.Should().HaveCount(3);
        result.Errors.Should().OnlyContain(e => e.Reason == "Queue is at maximum capacity");
        _jobRepoMock.Verify(
            r => r.UpdateAsync(It.IsAny<ProcessingJob>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_MixedJobs_ReportsCorrectCounts()
    {
        var retryableJob = CreateFailedJob();
        var maxRetriedJob = CreateFailedJob(retryCount: 3, maxRetries: 3);

        _jobRepoMock
            .Setup(r => r.GetAllByStatusAsync(JobStatus.Failed, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProcessingJob> { retryableJob, maxRetriedJob });
        _jobRepoMock
            .Setup(r => r.ExistsByPdfDocumentIdAsync(retryableJob.PdfDocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _handler.Handle(
            new BulkReindexFailedCommand(Guid.NewGuid()), CancellationToken.None);

        result.EnqueuedCount.Should().Be(1);
        result.SkippedCount.Should().Be(1);
        result.Errors.Should().HaveCount(1);
    }

    private static ProcessingJob CreateFailedJob(int retryCount = 0, int maxRetries = 3)
    {
        var job = ProcessingJob.Create(Guid.NewGuid(), Guid.NewGuid(), (int)ProcessingPriority.Normal, 0);
        job.Start();
        job.Fail("Test failure");

        // Set retry count via reflection if needed
        if (retryCount > 0)
        {
            var retryProp = typeof(ProcessingJob).GetProperty("RetryCount");
            retryProp!.SetValue(job, retryCount);
        }

        if (maxRetries != 3)
        {
            var maxProp = typeof(ProcessingJob).GetProperty("MaxRetries");
            maxProp!.SetValue(job, maxRetries);
        }

        return job;
    }
}
