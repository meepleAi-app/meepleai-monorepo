using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.DocumentProcessing;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Infrastructure.Services;

/// <summary>
/// Tests for ProcessingQueueMonitorService.RecoverStuckJobAsync — the #2683 auto-recovery
/// that requeues jobs orphaned in Processing (e.g. after an API restart mid-pipeline).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Issue", "2683")]
public sealed class ProcessingQueueMonitorRecoveryTests
{
    private static MeepleAiDbContext CreateDb(string name)
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"StuckRecovery_{name}_{Guid.NewGuid()}")
            .Options;
        return new MeepleAiDbContext(options, Mock.Of<IMediator>(), Mock.Of<IDomainEventCollector>());
    }

    private static ProcessingQueueMonitorService CreateService()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        return new ProcessingQueueMonitorService(
            Mock.Of<IServiceScopeFactory>(),
            config,
            Mock.Of<ILogger<ProcessingQueueMonitorService>>());
    }

    private static (PdfDocumentEntity pdf, ProcessingJobEntity job) Seed(
        MeepleAiDbContext db, string state, int retryCount, int maxRetries, string jobStatus = "Processing")
    {
        var pdf = new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            FileName = "carcassone_rulebook.pdf",
            FilePath = "pdfs/x/y_carcassone_rulebook.pdf",
            UploadedByUserId = Guid.NewGuid(),
            UploadedAt = DateTime.UtcNow.AddHours(-6),
            ProcessingState = state,
            Language = "en",
        };
        var job = new ProcessingJobEntity
        {
            Id = Guid.NewGuid(),
            PdfDocumentId = pdf.Id,
            UserId = pdf.UploadedByUserId,
            Status = jobStatus,
            Priority = 0,
            CurrentStep = "Embedding",
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-6),
            StartedAt = DateTimeOffset.UtcNow.AddHours(-6),
            RetryCount = retryCount,
            MaxRetries = maxRetries,
            PdfDocument = pdf,
        };
        db.PdfDocuments.Add(pdf);
        db.Set<ProcessingJobEntity>().Add(job);
        return (pdf, job);
    }

    [Fact]
    public async Task RecoverStuckJobAsync_UnderMaxRetries_RequeuesAndResetsPdfToPending()
    {
        await using var db = CreateDb(nameof(RecoverStuckJobAsync_UnderMaxRetries_RequeuesAndResetsPdfToPending));
        var (pdf, job) = Seed(db, nameof(PdfProcessingState.Embedding), retryCount: 0, maxRetries: 3);
        db.TextChunks.AddRange(
            new TextChunkEntity { Id = Guid.NewGuid(), GameId = Guid.NewGuid(), PdfDocumentId = pdf.Id, Content = "partial", ChunkIndex = 0, CharacterCount = 7, CreatedAt = DateTime.UtcNow },
            new TextChunkEntity { Id = Guid.NewGuid(), GameId = Guid.NewGuid(), PdfDocumentId = pdf.Id, Content = "partial2", ChunkIndex = 1, CharacterCount = 8, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        await CreateService().RecoverStuckJobAsync(db, job.Id, pdf.FileName, 350.0, CancellationToken.None);

        var reloadedJob = await db.Set<ProcessingJobEntity>().AsNoTracking().FirstAsync(j => j.Id == job.Id);
        var reloadedPdf = await db.PdfDocuments.AsNoTracking().FirstAsync(p => p.Id == pdf.Id);
        reloadedJob.Status.Should().Be("Queued");
        reloadedJob.RetryCount.Should().Be(1, "the recovery bumps RetryCount for loop-prevention");
        reloadedJob.StartedAt.Should().BeNull();
        reloadedPdf.ProcessingState.Should().Be(nameof(PdfProcessingState.Pending));
        reloadedPdf.ProcessingError.Should().BeNull();
        (await db.TextChunks.CountAsync(c => c.PdfDocumentId == pdf.Id))
            .Should().Be(0, "partial chunks are cleared before re-processing");
    }

    [Fact]
    public async Task RecoverStuckJobAsync_AtMaxRetries_MarksJobAndPdfFailed()
    {
        await using var db = CreateDb(nameof(RecoverStuckJobAsync_AtMaxRetries_MarksJobAndPdfFailed));
        var (pdf, job) = Seed(db, nameof(PdfProcessingState.Embedding), retryCount: 3, maxRetries: 3);
        await db.SaveChangesAsync();

        await CreateService().RecoverStuckJobAsync(db, job.Id, pdf.FileName, 400.0, CancellationToken.None);

        var reloadedJob = await db.Set<ProcessingJobEntity>().AsNoTracking().FirstAsync(j => j.Id == job.Id);
        var reloadedPdf = await db.PdfDocuments.AsNoTracking().FirstAsync(p => p.Id == pdf.Id);
        reloadedJob.Status.Should().Be("Failed", "recovery is exhausted — do not loop forever");
        reloadedJob.CompletedAt.Should().NotBeNull();
        reloadedJob.ErrorMessage.Should().Contain("exhausted");
        reloadedPdf.ProcessingState.Should().Be(nameof(PdfProcessingState.Failed));
        reloadedPdf.FailedAtState.Should().Be(nameof(PdfProcessingState.Embedding), "the state it was stuck in is preserved");
    }

    [Fact]
    public async Task RecoverStuckJobAsync_JobNoLongerProcessing_IsNoOp()
    {
        await using var db = CreateDb(nameof(RecoverStuckJobAsync_JobNoLongerProcessing_IsNoOp));
        // Status changed to Completed between detection and recovery — must not touch it.
        var (pdf, job) = Seed(db, nameof(PdfProcessingState.Ready), retryCount: 0, maxRetries: 3, jobStatus: "Completed");
        await db.SaveChangesAsync();

        await CreateService().RecoverStuckJobAsync(db, job.Id, pdf.FileName, 350.0, CancellationToken.None);

        var reloadedJob = await db.Set<ProcessingJobEntity>().AsNoTracking().FirstAsync(j => j.Id == job.Id);
        var reloadedPdf = await db.PdfDocuments.AsNoTracking().FirstAsync(p => p.Id == pdf.Id);
        reloadedJob.Status.Should().Be("Completed", "only Processing jobs are recovered");
        reloadedPdf.ProcessingState.Should().Be(nameof(PdfProcessingState.Ready));
    }
}
