using Api.BoundedContexts.DocumentProcessing.Application.Jobs;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;

/// <summary>
/// Handles <see cref="CloseNotClaimedJobsCommand"/> (#4443 R6). No data migration: an admin runs it,
/// first as a dry run.
/// </summary>
internal sealed class CloseNotClaimedJobsCommandHandler : ICommandHandler<CloseNotClaimedJobsCommand, CloseNotClaimedJobsResult>
{
    /// <summary>The message the worker wrote before R4 (<c>PdfProcessingQuartzJob</c>).</summary>
    internal const string LegacyNotClaimedMessagePrefix = "Pipeline did not process the document (outcome: SkippedNotClaimed)";

    private readonly MeepleAiDbContext _db;
    private readonly ILogger<CloseNotClaimedJobsCommandHandler> _logger;

    public CloseNotClaimedJobsCommandHandler(MeepleAiDbContext db, ILogger<CloseNotClaimedJobsCommandHandler> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CloseNotClaimedJobsResult> Handle(CloseNotClaimedJobsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var failed = nameof(JobStatus.Failed);
        var ready = nameof(PdfProcessingState.Ready);
        // Only on Ready documents: there the job's «failure» is certainly someone else's success. On a
        // document that is not Ready the job may be the only trace of a real problem, so it stays.
        var jobs = await _db.ProcessingJobs
            .AsTracking()
            .Where(j => j.Status == failed
                        && j.ErrorMessage != null
                        && EF.Functions.Like(j.ErrorMessage, LegacyNotClaimedMessagePrefix + "%")
                        && j.PdfDocument.ProcessingState == ready)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        if (command.DryRun || jobs.Count == 0)
        {
            _logger.LogInformation("Not-claimed jobs to close: {Matched} (dry run: {DryRun})", jobs.Count, command.DryRun);
            return new CloseNotClaimedJobsResult(command.DryRun, jobs.Count, 0);
        }

        foreach (var job in jobs)
        {
            job.Status = nameof(JobStatus.Cancelled);
            job.ErrorMessage = PdfProcessingQuartzJob.NotClaimedReason;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Closed {Closed} not-claimed jobs as Cancelled", jobs.Count);
        return new CloseNotClaimedJobsResult(false, jobs.Count, jobs.Count);
    }
}
