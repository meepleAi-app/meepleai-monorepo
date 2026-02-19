using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Domain.Entities;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class ProcessingJobTests
{
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 2, 19, 10, 0, 0, TimeSpan.Zero));

    #region Create Tests

    [Fact]
    public void Create_WithValidParameters_ReturnsJobWithQueuedStatus()
    {
        // Arrange
        var pdfId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Act
        var job = ProcessingJob.Create(pdfId, userId, 0, 0, _timeProvider);

        // Assert
        job.Id.Should().NotBeEmpty();
        job.PdfDocumentId.Should().Be(pdfId);
        job.UserId.Should().Be(userId);
        job.Status.Should().Be(JobStatus.Queued);
        job.Priority.Should().Be(0);
        job.CreatedAt.Should().Be(_timeProvider.GetUtcNow());
        job.StartedAt.Should().BeNull();
        job.CompletedAt.Should().BeNull();
        job.ErrorMessage.Should().BeNull();
        job.RetryCount.Should().Be(0);
        job.MaxRetries.Should().Be(ProcessingJob.DefaultMaxRetries);
        job.CurrentStep.Should().BeNull();
    }

    [Fact]
    public void Create_WithValidParameters_InitializesFivePipelineSteps()
    {
        // Arrange / Act
        var job = CreateValidJob();

        // Assert
        job.Steps.Should().HaveCount(5);
        job.Steps.Select(s => s.StepName).Should().ContainInOrder(
            ProcessingStepType.Upload,
            ProcessingStepType.Extract,
            ProcessingStepType.Chunk,
            ProcessingStepType.Embed,
            ProcessingStepType.Index);
        job.Steps.Should().AllSatisfy(s => s.Status.Should().Be(StepStatus.Pending));
    }

    [Fact]
    public void Create_WithValidParameters_RaisesJobQueuedEvent()
    {
        // Arrange
        var pdfId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Act
        var job = ProcessingJob.Create(pdfId, userId, 5, 0, _timeProvider);

        // Assert
        job.DomainEvents.Should().HaveCount(1);
        var evt = job.DomainEvents.First().Should().BeOfType<JobQueuedEvent>().Subject;
        evt.JobId.Should().Be(job.Id);
        evt.PdfDocumentId.Should().Be(pdfId);
        evt.UserId.Should().Be(userId);
        evt.Priority.Should().Be(5);
    }

    [Fact]
    public void Create_WithEmptyPdfDocumentId_ThrowsArgumentException()
    {
        // Act
        var action = () => ProcessingJob.Create(Guid.Empty, Guid.NewGuid(), 0, 0, _timeProvider);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("pdfDocumentId");
    }

    [Fact]
    public void Create_WithEmptyUserId_ThrowsArgumentException()
    {
        // Act
        var action = () => ProcessingJob.Create(Guid.NewGuid(), Guid.Empty, 0, 0, _timeProvider);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("userId");
    }

    [Fact]
    public void Create_WhenQueueIsFull_ThrowsInvalidOperationException()
    {
        // Act
        var action = () => ProcessingJob.Create(Guid.NewGuid(), Guid.NewGuid(), 0, ProcessingJob.MaxQueueSize, _timeProvider);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Queue is full*");
    }

    [Fact]
    public void Create_WhenQueueAtCapacityMinusOne_Succeeds()
    {
        // Act
        var job = ProcessingJob.Create(Guid.NewGuid(), Guid.NewGuid(), 0, ProcessingJob.MaxQueueSize - 1, _timeProvider);

        // Assert
        job.Status.Should().Be(JobStatus.Queued);
    }

    #endregion

    #region Start Tests

    [Fact]
    public void Start_WhenQueued_TransitionsToProcessing()
    {
        // Arrange
        var job = CreateValidJob();
        job.ClearDomainEvents();

        // Act
        job.Start(_timeProvider);

        // Assert
        job.Status.Should().Be(JobStatus.Processing);
        job.StartedAt.Should().Be(_timeProvider.GetUtcNow());
        job.CurrentStep.Should().Be(ProcessingStepType.Upload);
    }

    [Fact]
    public void Start_WhenQueued_RaisesJobStartedEvent()
    {
        // Arrange
        var job = CreateValidJob();
        job.ClearDomainEvents();

        // Act
        job.Start(_timeProvider);

        // Assert
        job.DomainEvents.Should().HaveCount(1);
        var evt = job.DomainEvents.First().Should().BeOfType<JobStartedEvent>().Subject;
        evt.JobId.Should().Be(job.Id);
        evt.PdfDocumentId.Should().Be(job.PdfDocumentId);
    }

    [Fact]
    public void Start_WhenNotQueued_ThrowsInvalidOperationException()
    {
        // Arrange
        var job = CreateValidJob();
        job.Start(_timeProvider);
        job.ClearDomainEvents();

        // Act
        var action = () => job.Start(_timeProvider);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot start job*");
    }

    #endregion

    #region StartStep Tests

    [Fact]
    public void StartStep_WhenProcessing_StartsTheStep()
    {
        // Arrange
        var job = CreateProcessingJob();

        // Act
        job.StartStep(ProcessingStepType.Upload, _timeProvider);

        // Assert
        job.CurrentStep.Should().Be(ProcessingStepType.Upload);
        var step = job.Steps.First(s => s.StepName == ProcessingStepType.Upload);
        step.Status.Should().Be(StepStatus.Running);
        step.StartedAt.Should().Be(_timeProvider.GetUtcNow());
    }

    [Fact]
    public void StartStep_WhenNotProcessing_ThrowsInvalidOperationException()
    {
        // Arrange
        var job = CreateValidJob();

        // Act
        var action = () => job.StartStep(ProcessingStepType.Upload, _timeProvider);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot start step*");
    }

    #endregion

    #region CompleteStep Tests

    [Fact]
    public void CompleteStep_WhenRunning_CompletesTheStep()
    {
        // Arrange
        var job = CreateProcessingJob();
        job.StartStep(ProcessingStepType.Upload, _timeProvider);
        _timeProvider.Advance(TimeSpan.FromSeconds(30));
        job.ClearDomainEvents();

        // Act
        job.CompleteStep(ProcessingStepType.Upload, "{\"size\":1024}", _timeProvider);

        // Assert
        var step = job.Steps.First(s => s.StepName == ProcessingStepType.Upload);
        step.Status.Should().Be(StepStatus.Completed);
        step.Duration.Should().Be(TimeSpan.FromSeconds(30));
        step.MetadataJson.Should().Be("{\"size\":1024}");
    }

    [Fact]
    public void CompleteStep_RaisesJobStepCompletedEvent()
    {
        // Arrange
        var job = CreateProcessingJob();
        job.StartStep(ProcessingStepType.Upload, _timeProvider);
        _timeProvider.Advance(TimeSpan.FromSeconds(10));
        job.ClearDomainEvents();

        // Act
        job.CompleteStep(ProcessingStepType.Upload, null, _timeProvider);

        // Assert
        job.DomainEvents.Should().HaveCount(1);
        var evt = job.DomainEvents.First().Should().BeOfType<JobStepCompletedEvent>().Subject;
        evt.JobId.Should().Be(job.Id);
        evt.StepType.Should().Be(ProcessingStepType.Upload);
        evt.Duration.Should().Be(TimeSpan.FromSeconds(10));
    }

    #endregion

    #region Complete Tests

    [Fact]
    public void Complete_WhenProcessing_TransitionsToCompleted()
    {
        // Arrange
        var job = CreateProcessingJob();
        _timeProvider.Advance(TimeSpan.FromMinutes(5));
        job.ClearDomainEvents();

        // Act
        job.Complete(_timeProvider);

        // Assert
        job.Status.Should().Be(JobStatus.Completed);
        job.CompletedAt.Should().Be(_timeProvider.GetUtcNow());
        job.CurrentStep.Should().BeNull();
    }

    [Fact]
    public void Complete_RaisesJobCompletedEvent()
    {
        // Arrange
        var job = CreateProcessingJob();
        _timeProvider.Advance(TimeSpan.FromMinutes(5));
        job.ClearDomainEvents();

        // Act
        job.Complete(_timeProvider);

        // Assert
        job.DomainEvents.Should().HaveCount(1);
        var evt = job.DomainEvents.First().Should().BeOfType<JobCompletedEvent>().Subject;
        evt.JobId.Should().Be(job.Id);
        evt.UserId.Should().Be(job.UserId);
        evt.TotalDuration.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void Complete_WhenNotProcessing_ThrowsInvalidOperationException()
    {
        // Arrange
        var job = CreateValidJob();

        // Act
        var action = () => job.Complete(_timeProvider);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot complete job*");
    }

    #endregion

    #region Fail Tests

    [Fact]
    public void Fail_WhenProcessing_TransitionsToFailed()
    {
        // Arrange
        var job = CreateProcessingJob();
        job.StartStep(ProcessingStepType.Extract, _timeProvider);
        _timeProvider.Advance(TimeSpan.FromSeconds(5));
        job.ClearDomainEvents();

        // Act
        job.Fail("Extraction failed: corrupt PDF", _timeProvider);

        // Assert
        job.Status.Should().Be(JobStatus.Failed);
        job.CompletedAt.Should().Be(_timeProvider.GetUtcNow());
        job.ErrorMessage.Should().Be("Extraction failed: corrupt PDF");
    }

    [Fact]
    public void Fail_FailsCurrentRunningStep()
    {
        // Arrange
        var job = CreateProcessingJob();
        job.StartStep(ProcessingStepType.Extract, _timeProvider);
        job.ClearDomainEvents();

        // Act
        job.Fail("Error", _timeProvider);

        // Assert
        var step = job.Steps.First(s => s.StepName == ProcessingStepType.Extract);
        step.Status.Should().Be(StepStatus.Failed);
    }

    [Fact]
    public void Fail_RaisesJobFailedEvent()
    {
        // Arrange
        var job = CreateProcessingJob();
        job.StartStep(ProcessingStepType.Chunk, _timeProvider);
        job.ClearDomainEvents();

        // Act
        job.Fail("Chunk error", _timeProvider);

        // Assert
        job.DomainEvents.Should().HaveCount(1);
        var evt = job.DomainEvents.First().Should().BeOfType<JobFailedEvent>().Subject;
        evt.JobId.Should().Be(job.Id);
        evt.ErrorMessage.Should().Be("Chunk error");
        evt.FailedAtStep.Should().Be(ProcessingStepType.Chunk);
        evt.RetryCount.Should().Be(0);
    }

    [Fact]
    public void Fail_WhenNotProcessing_ThrowsInvalidOperationException()
    {
        // Arrange
        var job = CreateValidJob();

        // Act
        var action = () => job.Fail("Error", _timeProvider);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot fail job*");
    }

    #endregion

    #region Cancel Tests

    [Fact]
    public void Cancel_WhenQueued_TransitionsToCancelled()
    {
        // Arrange
        var job = CreateValidJob();
        job.ClearDomainEvents();

        // Act
        job.Cancel(_timeProvider);

        // Assert
        job.Status.Should().Be(JobStatus.Cancelled);
        job.CompletedAt.Should().Be(_timeProvider.GetUtcNow());
    }

    [Fact]
    public void Cancel_WhenProcessing_TransitionsToCancelled()
    {
        // Arrange
        var job = CreateProcessingJob();
        job.ClearDomainEvents();

        // Act
        job.Cancel(_timeProvider);

        // Assert
        job.Status.Should().Be(JobStatus.Cancelled);
    }

    [Fact]
    public void Cancel_RaisesJobCancelledEvent()
    {
        // Arrange
        var job = CreateValidJob();
        job.ClearDomainEvents();

        // Act
        job.Cancel(_timeProvider);

        // Assert
        job.DomainEvents.Should().HaveCount(1);
        var evt = job.DomainEvents.First().Should().BeOfType<JobCancelledEvent>().Subject;
        evt.JobId.Should().Be(job.Id);
        evt.UserId.Should().Be(job.UserId);
    }

    [Fact]
    public void Cancel_WhenCompleted_ThrowsInvalidOperationException()
    {
        // Arrange
        var job = CreateProcessingJob();
        job.Complete(_timeProvider);
        job.ClearDomainEvents();

        // Act
        var action = () => job.Cancel(_timeProvider);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot cancel job*");
    }

    [Fact]
    public void Cancel_WhenFailed_ThrowsInvalidOperationException()
    {
        // Arrange
        var job = CreateProcessingJob();
        job.Fail("Error", _timeProvider);
        job.ClearDomainEvents();

        // Act
        var action = () => job.Cancel(_timeProvider);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot cancel job*");
    }

    #endregion

    #region Retry Tests

    [Fact]
    public void Retry_WhenFailed_ResetsToQueued()
    {
        // Arrange
        var job = CreateFailedJob();

        // Act
        job.Retry(_timeProvider);

        // Assert
        job.Status.Should().Be(JobStatus.Queued);
        job.RetryCount.Should().Be(1);
        job.StartedAt.Should().BeNull();
        job.CompletedAt.Should().BeNull();
        job.ErrorMessage.Should().BeNull();
        job.CurrentStep.Should().BeNull();
    }

    [Fact]
    public void Retry_RaisesJobRetriedEvent()
    {
        // Arrange
        var job = CreateFailedJob();
        job.ClearDomainEvents();

        // Act
        job.Retry(_timeProvider);

        // Assert
        job.DomainEvents.Should().HaveCount(1);
        var evt = job.DomainEvents.First().Should().BeOfType<JobRetriedEvent>().Subject;
        evt.JobId.Should().Be(job.Id);
        evt.PdfDocumentId.Should().Be(job.PdfDocumentId);
        evt.RetryCount.Should().Be(1);
    }

    [Fact]
    public void Retry_ResetsAllSteps()
    {
        // Arrange
        var job = CreateFailedJob();

        // Act
        job.Retry(_timeProvider);

        // Assert
        job.Steps.Should().HaveCount(5);
        job.Steps.Should().AllSatisfy(s => s.Status.Should().Be(StepStatus.Pending));
    }

    [Fact]
    public void Retry_WhenMaxRetriesExceeded_ThrowsInvalidOperationException()
    {
        // Arrange
        var job = CreateFailedJob();

        // Exhaust all retries
        for (var i = 0; i < ProcessingJob.DefaultMaxRetries; i++)
        {
            job.Retry(_timeProvider);
            job.Start(_timeProvider);
            job.Fail("Error", _timeProvider);
        }

        // Act
        var action = () => job.Retry(_timeProvider);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*exceeded maximum retries*");
    }

    [Fact]
    public void Retry_WhenNotFailed_ThrowsInvalidOperationException()
    {
        // Arrange
        var job = CreateValidJob();

        // Act
        var action = () => job.Retry(_timeProvider);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot retry job*");
    }

    #endregion

    #region UpdatePriority Tests

    [Fact]
    public void UpdatePriority_WhenQueued_UpdatesPriority()
    {
        // Arrange
        var job = CreateValidJob();
        job.ClearDomainEvents();

        // Act
        job.UpdatePriority(10);

        // Assert
        job.Priority.Should().Be(10);
    }

    [Fact]
    public void UpdatePriority_RaisesJobPriorityChangedEvent()
    {
        // Arrange
        var job = ProcessingJob.Create(Guid.NewGuid(), Guid.NewGuid(), 5, 0, _timeProvider);
        job.ClearDomainEvents();

        // Act
        job.UpdatePriority(1);

        // Assert
        job.DomainEvents.Should().HaveCount(1);
        var evt = job.DomainEvents.First().Should().BeOfType<JobPriorityChangedEvent>().Subject;
        evt.OldPriority.Should().Be(5);
        evt.NewPriority.Should().Be(1);
    }

    [Fact]
    public void UpdatePriority_WhenNotQueued_ThrowsInvalidOperationException()
    {
        // Arrange
        var job = CreateProcessingJob();

        // Act
        var action = () => job.UpdatePriority(10);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot change priority*");
    }

    #endregion

    #region CanRetry Tests

    [Fact]
    public void CanRetry_WhenFailedAndRetriesRemain_ReturnsTrue()
    {
        // Arrange
        var job = CreateFailedJob();

        // Assert
        job.CanRetry.Should().BeTrue();
    }

    [Fact]
    public void CanRetry_WhenNotFailed_ReturnsFalse()
    {
        // Arrange
        var job = CreateValidJob();

        // Assert
        job.CanRetry.Should().BeFalse();
    }

    [Fact]
    public void CanRetry_WhenMaxRetriesExhausted_ReturnsFalse()
    {
        // Arrange
        var job = CreateFailedJob();

        for (var i = 0; i < ProcessingJob.DefaultMaxRetries; i++)
        {
            job.Retry(_timeProvider);
            job.Start(_timeProvider);
            job.Fail("Error", _timeProvider);
        }

        // Assert
        job.CanRetry.Should().BeFalse();
    }

    #endregion

    #region AddStepLog Tests

    [Fact]
    public void AddStepLog_AddsLogEntryToStep()
    {
        // Arrange
        var job = CreateProcessingJob();
        job.StartStep(ProcessingStepType.Upload, _timeProvider);

        // Act
        job.AddStepLog(ProcessingStepType.Upload, StepLogLevel.Info, "Processing page 1 of 5", _timeProvider);

        // Assert
        var step = job.Steps.First(s => s.StepName == ProcessingStepType.Upload);
        step.LogEntries.Should().HaveCount(1);
        step.LogEntries.First().Level.Should().Be(StepLogLevel.Info);
        step.LogEntries.First().Message.Should().Be("Processing page 1 of 5");
    }

    #endregion

    #region SkipStep Tests

    [Fact]
    public void SkipStep_WhenProcessing_SkipsTheStep()
    {
        // Arrange
        var job = CreateProcessingJob();

        // Act
        job.SkipStep(ProcessingStepType.Upload);

        // Assert
        var step = job.Steps.First(s => s.StepName == ProcessingStepType.Upload);
        step.Status.Should().Be(StepStatus.Skipped);
    }

    [Fact]
    public void SkipStep_WhenNotProcessing_ThrowsInvalidOperationException()
    {
        // Arrange
        var job = CreateValidJob();

        // Act
        var action = () => job.SkipStep(ProcessingStepType.Upload);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot skip step*");
    }

    #endregion

    #region Helper Methods

    private ProcessingJob CreateValidJob()
    {
        return ProcessingJob.Create(Guid.NewGuid(), Guid.NewGuid(), 0, 0, _timeProvider);
    }

    private ProcessingJob CreateProcessingJob()
    {
        var job = CreateValidJob();
        job.Start(_timeProvider);
        return job;
    }

    private ProcessingJob CreateFailedJob()
    {
        var job = CreateProcessingJob();
        job.StartStep(ProcessingStepType.Upload, _timeProvider);
        job.Fail("Test error", _timeProvider);
        return job;
    }

    #endregion
}
