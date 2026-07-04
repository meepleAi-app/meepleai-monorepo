using Api.BoundedContexts.DocumentProcessing.Application.Jobs;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.DocumentProcessing;
using Api.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Quartz;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Jobs;

/// <summary>
/// Regression tests for Issue #2661.
///
/// The pipeline (<see cref="IPdfProcessingPipelineService"/>) populates
/// <c>PdfDocument.PageCount</c> and calls SaveChangesAsync on its own scoped
/// DbContext instance. The job holds a separately-tracked copy of the same
/// PdfDocument, so that copy stays stale (PageCount == null) unless it is reloaded.
/// Before the fix, the job passed <c>PageCount ?? 0</c> == 0 into
/// <see cref="IProcessingMetricsService.RecordStepDurationAsync"/>, tripping its
/// "Page count must be positive" guard and silently dropping every step metric.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class PdfProcessingQuartzJobTests
{
    private const int PersistedPageCount = 42;

    private static readonly string[] StepNames =
        { "Upload", "Extract", "Chunk", "Embed", "Index" };

    [Fact]
    public async Task Execute_ReloadsPdfDocAfterPipeline_PassesPersistedPageCountToMetrics()
    {
        // Arrange — two DbContexts over ONE shared in-memory store (same database name).
        // jobContext is what the job holds; pipelineContext simulates the pipeline's own
        // scoped context that persists PageCount out-of-band.
        var dbName = $"issue-2661-{Guid.NewGuid()}";
        using var jobContext = TestDbContextFactory.CreateInMemoryDbContext(dbName);
        using var pipelineContext = TestDbContextFactory.CreateInMemoryDbContext(dbName);

        var pdfId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var jobId = Guid.NewGuid();

        // PDF seeded with PageCount == null: the exact stale/unpopulated state the job
        // observes before the pipeline runs.
        jobContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            FileName = "rules.pdf",
            FilePath = "/tmp/rules.pdf",
            FileSizeBytes = 2048,
            UploadedByUserId = userId,
            ProcessingState = "Pending",
            PageCount = null,
        });
        jobContext.ProcessingJobs.Add(new ProcessingJobEntity
        {
            Id = jobId,
            PdfDocumentId = pdfId,
            UserId = userId,
            Status = nameof(JobStatus.Queued),
            Priority = 0,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        foreach (var stepName in StepNames)
        {
            jobContext.ProcessingSteps.Add(new ProcessingStepEntity
            {
                Id = Guid.NewGuid(),
                ProcessingJobId = jobId,
                StepName = stepName,
                Status = nameof(StepStatus.Pending),
            });
        }
        await jobContext.SaveChangesAsync();

        // Pipeline mock: simulate ExtractTextAsync writing PageCount via its OWN context,
        // committing to the shared store. This is what leaves the job's tracked copy stale.
        var pipeline = new Mock<IPdfProcessingPipelineService>();
        pipeline
            .Setup(p => p.ProcessAsync(pdfId, It.IsAny<string>(), userId, It.IsAny<CancellationToken>()))
            .Returns(async (Guid id, string _, Guid _, CancellationToken innerCt) =>
            {
                var doc = await pipelineContext.PdfDocuments.AsTracking()
                    .FirstAsync(d => d.Id == id, innerCt);
                doc.PageCount = PersistedPageCount;
                await pipelineContext.SaveChangesAsync(innerCt);
            });

        var metrics = new Mock<IProcessingMetricsService>();
        var stream = new Mock<IQueueStreamService>();

        var services = new ServiceCollection();
        services.AddSingleton(pipeline.Object);
        services.AddSingleton(metrics.Object);
        services.AddSingleton(stream.Object);
        using var serviceProvider = services.BuildServiceProvider();

        var job = new PdfProcessingQuartzJob(
            jobContext,
            serviceProvider,
            NullLogger<PdfProcessingQuartzJob>.Instance,
            TimeProvider.System);

        var executionContext = new Mock<IJobExecutionContext>();
        executionContext.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);

        // Act
        await job.Execute(executionContext.Object);

        // Assert — after the reload, every step metric receives the value the pipeline
        // persisted (42), never the stale 0 that would trip the guard.
        metrics.Verify(m => m.RecordStepDurationAsync(
                pdfId,
                It.IsAny<PdfProcessingState>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<long>(),
                PersistedPageCount,
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(StepNames.Length));

        metrics.Verify(m => m.RecordStepDurationAsync(
                It.IsAny<Guid>(),
                It.IsAny<PdfProcessingState>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<long>(),
                0,
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
