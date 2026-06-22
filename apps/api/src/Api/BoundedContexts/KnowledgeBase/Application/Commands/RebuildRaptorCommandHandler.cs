using Api.BoundedContexts.KnowledgeBase.Domain.Services.Enhancements;
using Api.Infrastructure;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Services;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Handler for <see cref="RebuildRaptorCommand"/>.
///
/// Rebuilds the RAPTOR hierarchical summary tree for all indexed PDFs of a game.
/// Enforces subscription-tier gating: free-tier users are rejected with 403 TIER_FEATURE_LOCKED.
///
/// Per-PDF process:
///   1. Load existing text chunks (already stored in text_chunks table after indexing)
///   2. Delete stale RaptorSummary rows for this PDF (cascade is also on DB, but we do it
///      explicitly here to avoid holding an open transaction through LLM calls)
///   3. Call IRaptorIndexer.BuildTreeAsync to generate new summaries via LLM
///   4. Persist the new RaptorSummaryEntity rows
///
/// Note: IRaptorIndexer is optional (nullable injection). If not registered — e.g., in
/// environments without LLM access — the handler returns "completed" with 0 nodes
/// and logs a warning.
///
/// Issue #903: SG2 — KB lifecycle con re-index.
/// </summary>
internal sealed class RebuildRaptorCommandHandler : ICommandHandler<RebuildRaptorCommand, KbJobResponse>
{
    private readonly MeepleAiDbContext _db;
    private readonly ITierEnforcementService _tierEnforcement;
    private readonly IRaptorIndexer? _raptorIndexer;
    private readonly ILogger<RebuildRaptorCommandHandler> _logger;

    public RebuildRaptorCommandHandler(
        MeepleAiDbContext db,
        ITierEnforcementService tierEnforcement,
        ILogger<RebuildRaptorCommandHandler> logger,
        IRaptorIndexer? raptorIndexer = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _tierEnforcement = tierEnforcement ?? throw new ArgumentNullException(nameof(tierEnforcement));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _raptorIndexer = raptorIndexer;
    }

    public async Task<KbJobResponse> Handle(RebuildRaptorCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // === Tier gate ===
        var canRebuild = await _tierEnforcement
            .CanPerformAsync(command.UserId, TierAction.RaptorRebuild, cancellationToken)
            .ConfigureAwait(false);

        if (!canRebuild)
        {
            throw new TierFeatureLockedException(
                feature: "RaptorRebuild",
                message: "RAPTOR summary rebuild requires a premium subscription.");
        }

        // If IRaptorIndexer is not available in this environment, return gracefully
        if (_raptorIndexer is null)
        {
            _logger.LogWarning(
                "RebuildRaptor: IRaptorIndexer not available (no LLM integration). Game {GameId}",
                command.GameId);
            return new KbJobResponse(JobId: Guid.NewGuid(), Status: "completed", PdfCount: 0);
        }

        // Load PDFs for the game that have text chunks stored (already indexed)
        var pdfIds = await _db.TextChunks
            .AsNoTracking()
            .Where(tc =>
                tc.SharedGameId == command.GameId || tc.GameId == command.GameId)
            .Select(tc => tc.PdfDocumentId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (pdfIds.Count == 0)
        {
            _logger.LogInformation(
                "RebuildRaptor: No indexed PDFs found for game {GameId}. Nothing to rebuild.",
                command.GameId);
            return new KbJobResponse(JobId: Guid.NewGuid(), Status: "completed", PdfCount: 0);
        }

        var jobId = Guid.NewGuid();
        var totalNodes = 0;

        _logger.LogInformation(
            "RebuildRaptor job {JobId}: rebuilding RAPTOR for {Count} PDFs — game {GameId}, user {UserId}",
            jobId, pdfIds.Count, command.GameId, command.UserId);

        foreach (var pdfId in pdfIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // Load ordered chunk texts for this PDF
                var chunkTexts = await _db.TextChunks
                    .AsNoTracking()
                    .Where(tc => tc.PdfDocumentId == pdfId)
                    .OrderBy(tc => tc.ChunkIndex)
                    .Select(tc => tc.Content)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (chunkTexts.Count <= 3)
                {
                    // RAPTOR is not useful for very small documents (fewer than 4 chunks)
                    _logger.LogDebug(
                        "RebuildRaptor job {JobId}: PDF {PdfId} has only {Count} chunks — skipping (min 4 required)",
                        jobId, pdfId, chunkTexts.Count);
                    continue;
                }

                // Delete stale summaries for this PDF before LLM calls
                // (the DB FK also cascades, but we do this explicitly for clarity)
                var stale = await _db.RaptorSummaries
                    .Where(r => r.PdfDocumentId == pdfId)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
                if (stale.Count > 0)
                {
                    _db.RaptorSummaries.RemoveRange(stale);
                    await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }

                // Build new RAPTOR tree
                var raptorResult = await _raptorIndexer
                    .BuildTreeAsync(pdfId, command.GameId, chunkTexts, maxLevels: 3, cancellationToken)
                    .ConfigureAwait(false);

                if (raptorResult.TotalNodes > 0)
                {
                    // Persist new summaries
                    foreach (var summary in raptorResult.Summaries)
                    {
                        _db.RaptorSummaries.Add(new RaptorSummaryEntity
                        {
                            Id = Guid.NewGuid(),
                            PdfDocumentId = pdfId,
                            GameId = command.GameId,
                            TreeLevel = summary.TreeLevel,
                            ClusterIndex = summary.ClusterIndex,
                            SummaryText = summary.SummaryText,
                            SourceChunkCount = summary.SourceChunkCount,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                    await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    totalNodes += raptorResult.TotalNodes;

                    _logger.LogInformation(
                        "RebuildRaptor job {JobId}: PDF {PdfId} — {Levels}-level tree, {Nodes} nodes",
                        jobId, pdfId, raptorResult.Levels, raptorResult.TotalNodes);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
#pragma warning disable CA1031 // RAPTOR is an optional enhancement; individual PDF failure must not abort the job.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _logger.LogError(ex,
                    "RebuildRaptor job {JobId}: error rebuilding PDF {PdfId} — continuing",
                    jobId, pdfId);
            }
        }

        _logger.LogInformation(
            "RebuildRaptor job {JobId}: completed — {TotalNodes} summary nodes for {PdfCount} PDFs (game {GameId})",
            jobId, totalNodes, pdfIds.Count, command.GameId);

        return new KbJobResponse(JobId: jobId, Status: "completed", PdfCount: pdfIds.Count);
    }
}
