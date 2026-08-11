using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;

/// <summary>
/// Handler for <see cref="BulkReindexReadyCommand"/>. Selects all <c>Ready</c> PDFs whose stored
/// <c>IndexerVersion</c> differs from the resolved target version and fans out the existing
/// <see cref="ReindexDocumentCommand"/> for each, paced against the processing-queue capacity.
/// SP3 RAG bulk re-index (issue #3269).
/// </summary>
internal sealed class BulkReindexReadyCommandHandler
    : ICommandHandler<BulkReindexReadyCommand, BulkReindexResult>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly IMediator _mediator;
    private readonly IProcessingJobRepository _jobRepository;
    private readonly IPdfExtractorHealthProbe _healthProbe;
    private readonly ILogger<BulkReindexReadyCommandHandler> _logger;

    public BulkReindexReadyCommandHandler(
        MeepleAiDbContext dbContext,
        IMediator mediator,
        IProcessingJobRepository jobRepository,
        IPdfExtractorHealthProbe healthProbe,
        ILogger<BulkReindexReadyCommandHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _jobRepository = jobRepository ?? throw new ArgumentNullException(nameof(jobRepository));
        _healthProbe = healthProbe ?? throw new ArgumentNullException(nameof(healthProbe));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<BulkReindexResult> Handle(
        BulkReindexReadyCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // C2 gate: refuse to run the bulk re-index against a stale/misconfigured extractor. A
        // silent flat-chunking regression would otherwise re-index the whole corpus into
        // headingless chunks with no error signal (#3269 safety hardening).
        if (!await _healthProbe.IsHealthyAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new ConflictException(
                "unstructured extractor unhealthy — refusing bulk reindex (would produce flat chunks)");
        }

        var targetVersion = string.IsNullOrWhiteSpace(command.TargetVersion)
            ? IndexerVersionRegistry.Current.Version
            : command.TargetVersion;

        // Select Ready PDFs whose stored version differs from the target, oldest first.
        // The explicit `IndexerVersion == null ||` clause makes the "include never-versioned docs"
        // intent unambiguous and stays correct even if this query is ever moved to raw SQL or
        // UseRelationalNulls(true), where Postgres three-valued logic would evaluate
        // `indexer_version <> 'v1.1'` as NULL (not TRUE) and silently drop NULL rows. (With EF
        // Core's default null semantics the clause is redundant, but harmless.)
        var candidateIds = await _dbContext.PdfDocuments
            .Where(p => p.ProcessingState == nameof(PdfProcessingState.Ready))
            .Where(p => p.IndexerVersion == null || p.IndexerVersion != targetVersion)
            // #3425: skip docs with no real source blob to re-extract. ReindexDocumentCommand
            // deletes chunks + resets Ready->Pending, then extraction fetches the blob and fails
            // (-> Failed) when there is none. Two source-less, IndexerVersion==null, Ready classes
            // would otherwise be selected and destructively re-processed on every version bump:
            //  1. ImportRagData docs: external pre-computed embeddings, FilePath = "".
            //  2. Demo-mock dogfood placeholders (seed/ prefix): FilePath is non-empty but points
            //     at no blob — same sibling guard used by ProcessPendingPdfs / StalePdfRecovery /
            //     SeedStateHealthCheck (PdfDocumentEntity.DemoMockFilePathPrefix, #3075).
            .Where(p => !string.IsNullOrEmpty(p.FilePath))
            .Where(p => !p.FilePath.StartsWith(PdfDocumentEntity.DemoMockFilePathPrefix))
            .OrderBy(p => p.UploadedAt)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Pace against the queue cap: ReindexDocumentCommand fans out to EnqueuePdfCommand →
        // ProcessingJob.Create, whose guard rejects when Queued + Processing >= MaxQueueSize and
        // throws a ConflictException. ReindexDocumentCommandHandler now rolls back its destructive
        // reset on that failure (issue #3269 B10) instead of leaving a phantom success, but we still
        // pace here so we don't dispatch reindexes that would only be rolled back. We MUST mirror
        // EnqueuePdfCommandHandler's EXACT capacity computation (Queued + Processing) so we never
        // dispatch beyond the real cap. Remaining docs are skipped with a retryable error so the
        // admin can re-run to drain the rest as the queue empties.
        var queuedCount = await _jobRepository
            .CountByStatusAsync(JobStatus.Queued, cancellationToken)
            .ConfigureAwait(false);
        var processingCount = await _jobRepository
            .CountByStatusAsync(JobStatus.Processing, cancellationToken)
            .ConfigureAwait(false);
        var availableSlots = ProcessingJob.MaxQueueSize - (queuedCount + processingCount);

        var enqueued = 0;
        var skipped = 0;
        var errors = new List<BulkReindexError>();

        foreach (var id in candidateIds)
        {
            if (enqueued >= availableSlots)
            {
                errors.Add(new BulkReindexError(id, "Queue is at maximum capacity — re-run to drain remaining"));
                skipped++;
                continue;
            }

            try
            {
                // Always pass targetVersion explicitly — never rely on ReindexDocumentCommand's
                // stored-wins (null → stored) fallback, which would leave old versions in place.
                await _mediator.Send(new ReindexDocumentCommand(id, targetVersion), cancellationToken)
                    .ConfigureAwait(false);
                enqueued++;
            }
            catch (ConflictException ex)
            {
                // Document went in-flight (or concurrent modification) between selection and
                // dispatch — skip it, the batch continues.
                errors.Add(new BulkReindexError(id, ex.Message));
                skipped++;
            }
        }

        _logger.LogInformation(
            "Bulk reindex of Ready PDFs to version {TargetVersion} (requested by {RequestedBy}): "
            + "{Enqueued} enqueued, {Skipped} skipped, {Candidates} candidates",
            targetVersion, command.RequestedBy, enqueued, skipped, candidateIds.Count);

        return new BulkReindexResult(enqueued, skipped, errors);
    }
}
