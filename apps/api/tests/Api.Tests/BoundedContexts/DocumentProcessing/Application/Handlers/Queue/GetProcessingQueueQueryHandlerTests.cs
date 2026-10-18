using Api.BoundedContexts.DocumentProcessing.Application.Queries.Queue;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.DocumentProcessing;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Handlers.Queue;

/// <summary>
/// #4443 R5: il worker preleva il job attivo con la priorità più alta (<c>PdfProcessingQuartzJob</c>,
/// <c>OrderByDescending(Priority)</c>), la lista li mostrava per priorità crescente. Un upload a
/// priorità 10 finiva sotto i job a priorità 0 e spariva dalla prima pagina del pannello.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "4443")]
public sealed class GetProcessingQueueQueryHandlerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ActiveJobs_AreListedByDescendingPriority_TheOrderTheWorkerPicksThem()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"queue-order-{Guid.NewGuid()}");
        AddJob(db, "low.pdf", priority: 0, createdAt: T0);
        AddJob(db, "urgent.pdf", priority: 30, createdAt: T0.AddMinutes(1));
        AddJob(db, "normal.pdf", priority: 10, createdAt: T0.AddMinutes(2));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await new GetProcessingQueueQueryHandler(db)
            .Handle(new GetProcessingQueueQuery(), TestContext.Current.CancellationToken);

        result.Jobs.Select(j => j.PdfFileName).Should().Equal("urgent.pdf", "normal.pdf", "low.pdf");
    }

    [Fact]
    public async Task ActiveJobs_WithTheSamePriority_NewestFirst()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"queue-order-{Guid.NewGuid()}");
        AddJob(db, "older.pdf", priority: 10, createdAt: T0);
        AddJob(db, "newer.pdf", priority: 10, createdAt: T0.AddMinutes(5));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await new GetProcessingQueueQueryHandler(db)
            .Handle(new GetProcessingQueueQuery(), TestContext.Current.CancellationToken);

        result.Jobs.Select(j => j.PdfFileName).Should().Equal("newer.pdf", "older.pdf");
    }

    [Fact]
    public async Task ActiveJobs_StayAboveFinishedOnes_WhateverTheirPriority()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"queue-order-{Guid.NewGuid()}");
        AddJob(db, "done-urgent.pdf", priority: 30, createdAt: T0.AddMinutes(9), status: JobStatus.Completed);
        AddJob(db, "queued-low.pdf", priority: 0, createdAt: T0);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await new GetProcessingQueueQueryHandler(db)
            .Handle(new GetProcessingQueueQuery(), TestContext.Current.CancellationToken);

        result.Jobs.Select(j => j.PdfFileName).Should().Equal("queued-low.pdf", "done-urgent.pdf");
    }

    private static void AddJob(
        MeepleAiDbContext db, string fileName, int priority, DateTimeOffset createdAt, JobStatus status = JobStatus.Queued)
    {
        var userId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();
        db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            FileName = fileName,
            FilePath = $"pdfs/game/{pdfId:N}_{fileName}",
            FileSizeBytes = 1,
            UploadedByUserId = userId,
            UploadedAt = createdAt.UtcDateTime,
        });
        db.ProcessingJobs.Add(new ProcessingJobEntity
        {
            Id = Guid.NewGuid(),
            PdfDocumentId = pdfId,
            UserId = userId,
            Status = status.ToString(),
            Priority = priority,
            CreatedAt = createdAt,
            MaxRetries = 3,
        });
    }
}
