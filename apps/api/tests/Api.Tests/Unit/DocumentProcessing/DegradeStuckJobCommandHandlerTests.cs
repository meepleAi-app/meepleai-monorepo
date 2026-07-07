using Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Api.Tests.Unit.DocumentProcessing;

/// <summary>
/// Unit tests for <see cref="DegradeStuckJobCommandHandler"/>.
/// Issue #2689: Validates that a job stuck in Processing is degraded to Failed (job + PDF)
/// using domain methods only (no direct field assignment).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class DegradeStuckJobCommandHandlerTests
{
    private static readonly FakeTimeProvider _timeProvider = new();

    // ──────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────

    /// <summary>Creates a ProcessingJob in Processing state via domain factory + Start().</summary>
    private static ProcessingJob MakeProcessingJob(Guid pdfDocumentId)
    {
        var job = ProcessingJob.Create(pdfDocumentId, Guid.NewGuid(), priority: 0, currentQueueSize: 0, _timeProvider);
        job.Start(_timeProvider);
        return job;
    }

    /// <summary>Creates a PdfDocument reconstituted in Embedding state.</summary>
    private static PdfDocument MakeEmbeddingPdf(Guid id)
    {
        return PdfDocument.Reconstitute(
            id: id,
            gameId: Guid.NewGuid(),
            fileName: new FileName("rulebook.pdf"),
            filePath: "/tmp/rulebook.pdf",
            fileSize: FileSize.OneKilobyte,
            uploadedByUserId: Guid.NewGuid(),
            uploadedAt: DateTime.UtcNow.AddHours(-2),
            processedAt: null,
            pageCount: null,
            processingError: null,
            language: LanguageCode.English,
            processingState: PdfProcessingState.Embedding);
    }

    private static DegradeStuckJobCommandHandler BuildHandler(
        IProcessingJobRepository jobRepo,
        IPdfDocumentRepository pdfRepo,
        IUnitOfWork unitOfWork) =>
        new(jobRepo, pdfRepo, unitOfWork, _timeProvider,
            NullLogger<DegradeStuckJobCommandHandler>.Instance);

    // ──────────────────────────────────────────────────────────────────
    // Test 1: happy path — job Processing + PDF Embedding → both Failed
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_JobStuckInProcessing_MarksJobAndPdfFailed_WithServiceCategory()
    {
        // Arrange
        var pdfId = Guid.NewGuid();
        var job = MakeProcessingJob(pdfId);
        var pdf = MakeEmbeddingPdf(pdfId);

        ProcessingJob? capturedJob = null;
        PdfDocument? capturedPdf = null;

        var jobRepo = new Mock<IProcessingJobRepository>(MockBehavior.Strict);
        jobRepo.Setup(r => r.GetByIdAsync(job.Id, It.IsAny<CancellationToken>()))
               .ReturnsAsync(job);
        jobRepo.Setup(r => r.UpdateAsync(It.IsAny<ProcessingJob>(), It.IsAny<CancellationToken>()))
               .Callback<ProcessingJob, CancellationToken>((j, _) => capturedJob = j)
               .Returns(Task.CompletedTask);

        var pdfRepo = new Mock<IPdfDocumentRepository>(MockBehavior.Strict);
        pdfRepo.Setup(r => r.GetByIdAsync(pdfId, It.IsAny<CancellationToken>()))
               .ReturnsAsync(pdf);
        pdfRepo.Setup(r => r.UpdateAsync(It.IsAny<PdfDocument>(), It.IsAny<CancellationToken>()))
               .Callback<PdfDocument, CancellationToken>((p, _) => capturedPdf = p)
               .Returns(Task.CompletedTask);

        var unitOfWork = new Mock<IUnitOfWork>(MockBehavior.Strict);
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
                  .ReturnsAsync(1);

        var handler = BuildHandler(jobRepo.Object, pdfRepo.Object, unitOfWork.Object);

        // Act
        var result = await handler.Handle(new DegradeStuckJobCommand(job.Id, 42.0), CancellationToken.None);

        // Assert — result
        Assert.True(result.Degraded);
        Assert.Equal("Degraded to Failed", result.Reason);

        // Assert — job was marked Failed via domain method
        Assert.NotNull(capturedJob);
        Assert.Equal(JobStatus.Failed, capturedJob.Status);
        Assert.Contains("42", capturedJob.ErrorMessage);

        // Assert — PDF was marked Failed with transient category and correct recovery point
        Assert.NotNull(capturedPdf);
        Assert.Equal(PdfProcessingState.Failed, capturedPdf.ProcessingState);
        Assert.Equal(ErrorCategory.Service, capturedPdf.ErrorCategory);
        Assert.Equal(PdfProcessingState.Embedding, capturedPdf.FailedAtState);
    }

    // ──────────────────────────────────────────────────────────────────
    // Test 2: idempotency — job already Failed → no-op, no writes
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_JobAlreadyFailed_ReturnsDegradedFalse_NoWrites()
    {
        // Arrange: drive job to Failed state before the handler sees it
        var pdfId = Guid.NewGuid();
        var job = MakeProcessingJob(pdfId);
        job.Fail("previous failure", _timeProvider); // Status → Failed

        var jobRepo = new Mock<IProcessingJobRepository>(MockBehavior.Strict);
        jobRepo.Setup(r => r.GetByIdAsync(job.Id, It.IsAny<CancellationToken>()))
               .ReturnsAsync(job);
        // No UpdateAsync setup → Strict mock throws if accidentally called

        var pdfRepo = new Mock<IPdfDocumentRepository>(MockBehavior.Strict);
        var unitOfWork = new Mock<IUnitOfWork>(MockBehavior.Strict);

        var handler = BuildHandler(jobRepo.Object, pdfRepo.Object, unitOfWork.Object);

        // Act
        var result = await handler.Handle(new DegradeStuckJobCommand(job.Id, 30.0), CancellationToken.None);

        // Assert
        Assert.False(result.Degraded);
        Assert.Contains("no longer Processing", result.Reason);

        // Verify no writes were made
        jobRepo.Verify(r => r.UpdateAsync(It.IsAny<ProcessingJob>(), It.IsAny<CancellationToken>()), Times.Never);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ──────────────────────────────────────────────────────────────────
    // Test 3: concurrency conflict → Degraded=false, no re-throw
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ConcurrencyConflict_ReturnsDegradedFalse_DoesNotRethrow()
    {
        // Arrange
        var pdfId = Guid.NewGuid();
        var job = MakeProcessingJob(pdfId);

        // Loose mocks: pdfRepo returns null so the PDF branch is skipped cleanly
        var jobRepo = new Mock<IProcessingJobRepository>(MockBehavior.Loose);
        jobRepo.Setup(r => r.GetByIdAsync(job.Id, It.IsAny<CancellationToken>()))
               .ReturnsAsync(job);
        jobRepo.Setup(r => r.UpdateAsync(It.IsAny<ProcessingJob>(), It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);

        var pdfRepo = new Mock<IPdfDocumentRepository>(MockBehavior.Loose);
        // Default Loose returns null → handler skips pdf.MarkAsFailed

        var unitOfWork = new Mock<IUnitOfWork>(MockBehavior.Strict);
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
                  .ThrowsAsync(new DbUpdateConcurrencyException("xmin conflict"));

        var handler = BuildHandler(jobRepo.Object, pdfRepo.Object, unitOfWork.Object);

        // Act — must NOT throw
        var result = await handler.Handle(new DegradeStuckJobCommand(job.Id, 15.0), CancellationToken.None);

        // Assert
        Assert.False(result.Degraded);
        Assert.Contains("Concurrency", result.Reason);
    }
}
