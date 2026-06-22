using Api.BoundedContexts.DocumentProcessing.Application.Queries.Queue;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.DocumentProcessing;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Handlers.Queue;

/// <summary>
/// Unit tests for GetLatestIngestionLogByDocumentIdQueryHandler.
/// Issue #1650: KB Ingestion log tab.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class GetLatestIngestionLogByDocumentIdQueryHandlerTests
{
    [Fact]
    public async Task Handle_NoJob_ReturnsNull()
    {
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        var sut = new GetLatestIngestionLogByDocumentIdQueryHandler(db);

        var result = await sut.Handle(
            new GetLatestIngestionLogByDocumentIdQuery(Guid.NewGuid()),
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_EmptyGuid_ReturnsNull()
    {
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        var sut = new GetLatestIngestionLogByDocumentIdQueryHandler(db);

        var result = await sut.Handle(
            new GetLatestIngestionLogByDocumentIdQuery(Guid.Empty),
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_SingleJob_ReturnsJobWithSteps()
    {
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var jobId = Guid.NewGuid();

        db.Set<PdfDocumentEntity>().Add(CreatePdfDocument(docId, userId));
        db.Set<ProcessingJobEntity>().Add(new ProcessingJobEntity
        {
            Id = jobId,
            PdfDocumentId = docId,
            UserId = userId,
            Status = "Completed",
            RetryCount = 0,
            MaxRetries = 3,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var sut = new GetLatestIngestionLogByDocumentIdQueryHandler(db);

        var result = await sut.Handle(
            new GetLatestIngestionLogByDocumentIdQuery(docId),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(jobId, result!.Id);
        Assert.Equal(docId, result.PdfDocumentId);
        Assert.False(result.CanRetry); // Completed status → cannot retry
    }

    [Fact]
    public async Task Handle_MultipleJobs_ReturnsMostRecent()
    {
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var olderJobId = Guid.NewGuid();
        var newerJobId = Guid.NewGuid();

        db.Set<PdfDocumentEntity>().Add(CreatePdfDocument(docId, userId));
        db.Set<ProcessingJobEntity>().Add(new ProcessingJobEntity
        {
            Id = olderJobId,
            PdfDocumentId = docId,
            UserId = userId,
            Status = "Completed",
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-2),
        });
        db.Set<ProcessingJobEntity>().Add(new ProcessingJobEntity
        {
            Id = newerJobId,
            PdfDocumentId = docId,
            UserId = userId,
            Status = "Completed",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var sut = new GetLatestIngestionLogByDocumentIdQueryHandler(db);

        var result = await sut.Handle(
            new GetLatestIngestionLogByDocumentIdQuery(docId),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(newerJobId, result!.Id);
    }

    [Fact]
    public async Task Handle_FailedJobWithRetryAvailable_SetsCanRetryTrue()
    {
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        var docId = Guid.NewGuid();

        db.Set<PdfDocumentEntity>().Add(CreatePdfDocument(docId, userId));
        db.Set<ProcessingJobEntity>().Add(new ProcessingJobEntity
        {
            Id = Guid.NewGuid(),
            PdfDocumentId = docId,
            UserId = userId,
            Status = "Failed",
            RetryCount = 1,
            MaxRetries = 3,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var sut = new GetLatestIngestionLogByDocumentIdQueryHandler(db);

        var result = await sut.Handle(
            new GetLatestIngestionLogByDocumentIdQuery(docId),
            CancellationToken.None);

        Assert.True(result!.CanRetry);
    }

    [Fact]
    public async Task Handle_FailedJobAtMaxRetries_SetsCanRetryFalse()
    {
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        var userId = Guid.NewGuid();
        var docId = Guid.NewGuid();

        db.Set<PdfDocumentEntity>().Add(CreatePdfDocument(docId, userId));
        db.Set<ProcessingJobEntity>().Add(new ProcessingJobEntity
        {
            Id = Guid.NewGuid(),
            PdfDocumentId = docId,
            UserId = userId,
            Status = "Failed",
            RetryCount = 3,
            MaxRetries = 3,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var sut = new GetLatestIngestionLogByDocumentIdQueryHandler(db);

        var result = await sut.Handle(
            new GetLatestIngestionLogByDocumentIdQuery(docId),
            CancellationToken.None);

        Assert.False(result!.CanRetry);
    }

    // ── Helpers ───────────────────────────────────────────────

    private static PdfDocumentEntity CreatePdfDocument(Guid id, Guid userId) =>
        new()
        {
            Id = id,
            FileName = $"test-{id:N}.pdf",
            FilePath = $"/test/test-{id:N}.pdf",
            FileSizeBytes = 1024,
            UploadedByUserId = userId,
            UploadedAt = DateTime.UtcNow,
        };
}
