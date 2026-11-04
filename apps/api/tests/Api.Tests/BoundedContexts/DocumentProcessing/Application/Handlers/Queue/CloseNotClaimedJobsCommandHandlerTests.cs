using Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;
using Api.BoundedContexts.DocumentProcessing.Application.Jobs;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.DocumentProcessing;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Handlers.Queue;

/// <summary>
/// #4443 R6: the jobs the worker left <c>Failed</c> with «SkippedNotClaimed» on documents that reached
/// <c>Ready</c> are closed as <c>Cancelled</c>, after a dry run, and only those.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "4443")]
public sealed class CloseNotClaimedJobsCommandHandlerTests : IDisposable
{
    private const string LegacyMessage = "Pipeline did not process the document (outcome: SkippedNotClaimed).";

    private readonly MeepleAiDbContext _db = TestDbContextFactory.CreateInMemoryDbContext($"close-not-claimed-{Guid.NewGuid()}");

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task DryRun_CountsTheMatches_AndChangesNothing()
    {
        var match = await SeedAsync(JobStatus.Failed, LegacyMessage, PdfProcessingState.Ready);

        var result = await Handler().Handle(new CloseNotClaimedJobsCommand(DryRun: true), TestContext.Current.CancellationToken);

        result.Should().Be(new CloseNotClaimedJobsResult(true, 1, 0));
        (await StatusOfAsync(match)).Should().Be(nameof(JobStatus.Failed));
    }

    [Fact]
    public async Task Run_ClosesOnlyTheFailedNotClaimedJobsOnReadyDocuments()
    {
        var match = await SeedAsync(JobStatus.Failed, LegacyMessage, PdfProcessingState.Ready);
        var notReady = await SeedAsync(JobStatus.Failed, LegacyMessage, PdfProcessingState.Failed);
        var otherFailure = await SeedAsync(JobStatus.Failed, "Failed at Extract: boom", PdfProcessingState.Ready);
        var completed = await SeedAsync(JobStatus.Completed, null, PdfProcessingState.Ready);

        var result = await Handler().Handle(new CloseNotClaimedJobsCommand(DryRun: false), TestContext.Current.CancellationToken);

        result.Should().Be(new CloseNotClaimedJobsResult(false, 1, 1));
        (await StatusOfAsync(match)).Should().Be(nameof(JobStatus.Cancelled));
        (await _db.ProcessingJobs.AsNoTracking().SingleAsync(j => j.Id == match, TestContext.Current.CancellationToken))
            .ErrorMessage.Should().Be(PdfProcessingQuartzJob.NotClaimedReason);
        (await StatusOfAsync(notReady)).Should().Be(nameof(JobStatus.Failed), "on a document that is not Ready the job may be the trace of a real problem");
        (await StatusOfAsync(otherFailure)).Should().Be(nameof(JobStatus.Failed));
        (await StatusOfAsync(completed)).Should().Be(nameof(JobStatus.Completed));
    }

    [Fact]
    public async Task Run_IsIdempotent()
    {
        await SeedAsync(JobStatus.Failed, LegacyMessage, PdfProcessingState.Ready);
        var ct = TestContext.Current.CancellationToken;

        await Handler().Handle(new CloseNotClaimedJobsCommand(DryRun: false), ct);
        var second = await Handler().Handle(new CloseNotClaimedJobsCommand(DryRun: false), ct);

        second.Should().Be(new CloseNotClaimedJobsResult(false, 0, 0));
    }

    private CloseNotClaimedJobsCommandHandler Handler() =>
        new(_db, NullLogger<CloseNotClaimedJobsCommandHandler>.Instance);

    private async Task<Guid> SeedAsync(JobStatus status, string? errorMessage, PdfProcessingState documentState)
    {
        var pdfId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        _db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            FileName = $"{pdfId:N}.pdf",
            FilePath = $"pdfs/game/{pdfId:N}.pdf",
            FileSizeBytes = 1,
            UploadedByUserId = userId,
            ProcessingState = documentState.ToString(),
        });
        _db.ProcessingJobs.Add(new ProcessingJobEntity
        {
            Id = jobId,
            PdfDocumentId = pdfId,
            UserId = userId,
            Status = status.ToString(),
            ErrorMessage = errorMessage,
            CreatedAt = DateTimeOffset.UtcNow,
            CompletedAt = status == JobStatus.Queued ? null : DateTimeOffset.UtcNow,
            MaxRetries = 3,
        });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        _db.ChangeTracker.Clear();
        return jobId;
    }

    private async Task<string> StatusOfAsync(Guid jobId) =>
        await _db.ProcessingJobs.AsNoTracking().Where(j => j.Id == jobId).Select(j => j.Status).SingleAsync(TestContext.Current.CancellationToken);
}
