using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Handlers.Queue;

/// <summary>
/// Unit tests for <see cref="BulkReindexReadyCommandHandler"/>. Issue #3269 (SP3 RAG bulk re-index).
/// Uses EF InMemory for the PDF selector + Moq for the fan-out mediator and the queue-capacity
/// repository. The Postgres null-comparison trap on the selector is proven separately by the
/// Testcontainers integration test.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "3269")]
public sealed class BulkReindexReadyCommandHandlerTests : IAsyncLifetime
{
    private const string CapacityError = "Queue is at maximum capacity — re-run to drain remaining";

    private MeepleAiDbContext _db = default!;
    private Mock<IMediator> _mediator = default!;
    private Mock<IProcessingJobRepository> _jobRepo = default!;
    private Mock<IPdfExtractorHealthProbe> _healthProbe = default!;

    public ValueTask InitializeAsync()
    {
        _db = TestDbContextFactory.CreateInMemoryDbContext($"bulk_reindex_ready_{Guid.NewGuid():N}");
        _mediator = new Mock<IMediator>();
        // ReindexDocumentCommand is a void ICommand (IRequest) → MediatR dispatches it via the
        // non-generic Task Send<TRequest>(TRequest, ct) overload, so the setup returns Task, not Task<Unit>.
        _mediator
            .Setup(m => m.Send(It.IsAny<ReindexDocumentCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _jobRepo = new Mock<IProcessingJobRepository>();
        _jobRepo
            .Setup(r => r.CountByStatusAsync(JobStatus.Queued, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Default: extractor is healthy so all existing scenarios exercise the selector/pacing
        // logic unimpeded. The unhealthy-gate scenario overrides this per-test.
        _healthProbe = new Mock<IPdfExtractorHealthProbe>();
        _healthProbe
            .Setup(p => p.IsHealthyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    private BulkReindexReadyCommandHandler CreateHandler() =>
        new(_db, _mediator.Object, _jobRepo.Object, _healthProbe.Object,
            NullLogger<BulkReindexReadyCommandHandler>.Instance);

    private async Task<PdfDocumentEntity> SeedPdfAsync(
        string state = "Ready",
        string? indexerVersion = null,
        DateTime? uploadedAt = null)
    {
        var pdf = new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            FileName = "test.pdf",
            FilePath = "/tmp/test.pdf",
            FileSizeBytes = 1024,
            ContentType = "application/pdf",
            UploadedByUserId = Guid.NewGuid(),
            UploadedAt = uploadedAt ?? DateTime.UtcNow,
            ProcessingState = state,
            IndexerVersion = indexerVersion,
        };
        _db.PdfDocuments.Add(pdf);
        await _db.SaveChangesAsync();
        return pdf;
    }

    [Fact]
    public async Task Handle_ReadyPdfWithOldVersion_SendsReindexWithExplicitCurrentVersion()
    {
        var pdf = await SeedPdfAsync(indexerVersion: "v1.0");
        var handler = CreateHandler();

        var result = await handler.Handle(
            new BulkReindexReadyCommand(Guid.NewGuid()), CancellationToken.None);

        _mediator.Verify(
            m => m.Send(
                It.Is<ReindexDocumentCommand>(c =>
                    c.PdfId == pdf.Id && c.IndexerVersion == IndexerVersionRegistry.Current.Version),
                It.IsAny<CancellationToken>()),
            Times.Once);
        // The current pipeline version is v1.1 (SP3 #3269). Assert the literal explicitly.
        IndexerVersionRegistry.Current.Version.Should().Be("v1.1");
        result.EnqueuedCount.Should().Be(1);
        result.SkippedCount.Should().Be(0);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ReadyPdfAlreadyAtTargetVersion_IsIdempotentSkip()
    {
        await SeedPdfAsync(indexerVersion: IndexerVersionRegistry.Current.Version);
        var handler = CreateHandler();

        var result = await handler.Handle(
            new BulkReindexReadyCommand(Guid.NewGuid()), CancellationToken.None);

        _mediator.Verify(
            m => m.Send(It.IsAny<ReindexDocumentCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
        result.EnqueuedCount.Should().Be(0);
        result.SkippedCount.Should().Be(0);
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("Failed")]
    [InlineData("Indexing")]
    public async Task Handle_NonReadyPdf_IsNotSelected(string state)
    {
        await SeedPdfAsync(state: state, indexerVersion: "v1.0");
        var handler = CreateHandler();

        var result = await handler.Handle(
            new BulkReindexReadyCommand(Guid.NewGuid()), CancellationToken.None);

        _mediator.Verify(
            m => m.Send(It.IsAny<ReindexDocumentCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
        result.EnqueuedCount.Should().Be(0);
        result.SkippedCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_QueueNearCapacity_PacesEnqueueAndSkipsRemainder()
    {
        // 2 free slots in the queue.
        _jobRepo
            .Setup(r => r.CountByStatusAsync(JobStatus.Queued, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProcessingJob.MaxQueueSize - 2);

        for (var i = 0; i < 5; i++)
        {
            await SeedPdfAsync(indexerVersion: "v1.0", uploadedAt: DateTime.UtcNow.AddMinutes(i));
        }

        var handler = CreateHandler();

        var result = await handler.Handle(
            new BulkReindexReadyCommand(Guid.NewGuid()), CancellationToken.None);

        _mediator.Verify(
            m => m.Send(It.IsAny<ReindexDocumentCommand>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        result.EnqueuedCount.Should().Be(2);
        result.SkippedCount.Should().Be(3);
        result.Errors.Should().HaveCount(3);
        result.Errors.Should().OnlyContain(e => e.Reason == CapacityError);
    }

    [Fact]
    public async Task Handle_QueueFullViaProcessingJobs_CountsProcessingAndSkipsAll()
    {
        // Queue is full via in-flight Processing jobs, with 0 Queued. This proves Processing is
        // counted toward capacity: if the handler only counted Queued, availableSlots would be
        // the full MaxQueueSize and every candidate would (phantom-)enqueue while ProcessingJob.Create
        // (inside the ReindexDocument→EnqueuePdf fan-out) would actually reject them.
        _jobRepo
            .Setup(r => r.CountByStatusAsync(JobStatus.Queued, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _jobRepo
            .Setup(r => r.CountByStatusAsync(JobStatus.Processing, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProcessingJob.MaxQueueSize);

        for (var i = 0; i < 3; i++)
        {
            await SeedPdfAsync(indexerVersion: "v1.0", uploadedAt: DateTime.UtcNow.AddMinutes(i));
        }

        var handler = CreateHandler();

        var result = await handler.Handle(
            new BulkReindexReadyCommand(Guid.NewGuid()), CancellationToken.None);

        _mediator.Verify(
            m => m.Send(It.IsAny<ReindexDocumentCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
        result.EnqueuedCount.Should().Be(0);
        result.SkippedCount.Should().Be(3);
        result.Errors.Should().HaveCount(3);
        result.Errors.Should().OnlyContain(e => e.Reason == CapacityError);
    }

    [Fact]
    public async Task Handle_ReindexThrowsConflict_SkipsThatIdAndContinues()
    {
        var pdfA = await SeedPdfAsync(indexerVersion: "v1.0", uploadedAt: DateTime.UtcNow.AddMinutes(1));
        var pdfB = await SeedPdfAsync(indexerVersion: "v1.0", uploadedAt: DateTime.UtcNow.AddMinutes(2));
        var pdfC = await SeedPdfAsync(indexerVersion: "v1.0", uploadedAt: DateTime.UtcNow.AddMinutes(3));

        // pdfB's reindex conflicts (e.g. concurrent in-flight); the batch must continue.
        _mediator
            .Setup(m => m.Send(
                It.Is<ReindexDocumentCommand>(c => c.PdfId == pdfB.Id),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConflictException("Document is currently being processed"));

        var handler = CreateHandler();

        var result = await handler.Handle(
            new BulkReindexReadyCommand(Guid.NewGuid()), CancellationToken.None);

        // All three were attempted.
        _mediator.Verify(
            m => m.Send(It.IsAny<ReindexDocumentCommand>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
        result.EnqueuedCount.Should().Be(2);
        result.SkippedCount.Should().Be(1);
        result.Errors.Should().ContainSingle(e => e.JobId == pdfB.Id);
        result.Errors.Should().OnlyContain(e => e.Reason.Contains("being processed"));

        // Sanity: A and C were sent.
        _mediator.Verify(
            m => m.Send(It.Is<ReindexDocumentCommand>(c => c.PdfId == pdfA.Id), It.IsAny<CancellationToken>()),
            Times.Once);
        _mediator.Verify(
            m => m.Send(It.Is<ReindexDocumentCommand>(c => c.PdfId == pdfC.Id), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_UnhealthyExtractor_ThrowsConflictAndSkipsSelectionEntirely()
    {
        // Even with a perfectly valid Ready/mismatched-version candidate in the DB, an unhealthy
        // extractor must short-circuit BEFORE any candidate selection or fan-out — refusing to
        // silently re-index the whole corpus into flat, headingless chunks (#3269 C2 gate).
        await SeedPdfAsync(indexerVersion: "v1.0");
        _healthProbe
            .Setup(p => p.IsHealthyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = CreateHandler();

        var act = () => handler.Handle(new BulkReindexReadyCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*unstructured extractor unhealthy*");

        _mediator.Verify(
            m => m.Send(It.IsAny<ReindexDocumentCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _jobRepo.Verify(
            r => r.CountByStatusAsync(It.IsAny<JobStatus>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
