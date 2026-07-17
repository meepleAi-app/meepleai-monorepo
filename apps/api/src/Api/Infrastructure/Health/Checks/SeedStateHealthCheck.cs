using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Api.Infrastructure.Health.Checks;

/// <summary>
/// Reports the RAG seed state — whether the database has been seeded with
/// PDF documents, text chunks, and vector embeddings.
///
/// Surfaces a coarse-grained <c>seed_state</c> field that lets monitoring
/// (and the developer hitting <c>/health/ready</c> after <c>make dev</c>) see
/// at a glance whether the stack is actually ready to serve RAG queries.
///
/// State machine (counts from <see cref="MeepleAiDbContext.PdfDocuments"/>,
/// <see cref="MeepleAiDbContext.TextChunks"/>,
/// <see cref="MeepleAiDbContext.VectorDocuments"/>):
///
/// <list type="bullet">
///   <item>
///     <description><c>empty</c> — no PDFs, no chunks, no embeddings.
///     `make dev` was just run and the runtime indexer has not produced
///     anything yet, or <c>make dev-from-snapshot</c> was never used.</description>
///   </item>
///   <item>
///     <description><c>indexing</c> — chunks and embeddings exist but at
///     least one PDF is still in a pre-<c>Ready</c> processing state
///     (Pending / Uploading / Extracting / Chunking / Embedding /
///     Indexing). Either runtime indexing is in flight or a bake is mid-flight.</description>
///   </item>
///   <item>
///     <description><c>partial_failed</c> — sidecar invariant is violated:
///     <c>chunk_count != embedding_count</c>, OR at least one PDF is in
///     state <c>Failed</c>. RAG returns degraded recall. Equivalent to
///     <c>snapshot-verify.sh</c> exit code 6 (#2126 D7) but applied to the
///     live DB. Note (#2675): <c>embedding_count</c> is the SUM of
///     <c>VectorDocument.ChunkCount</c> across VectorDocuments (chunks indexed),
///     not the VectorDocuments row-count (which is per-PDF), so it is directly
///     comparable to <c>chunk_count</c> from TextChunks.</description>
///   </item>
///   <item>
///     <description><c>ready</c> — every PDF is <c>Ready</c>, chunks and
///     embeddings agree, RAG can serve at full recall.</description>
///   </item>
/// </list>
///
/// Refs: #2126 Tier 1 D3, spec
/// docs/for-developers/specs/2026-04-10-seed-pdf-pre-indexed-design.md
/// </summary>
public sealed class SeedStateHealthCheck : IHealthCheck
{
    private readonly MeepleAiDbContext _db;
    private readonly ILogger<SeedStateHealthCheck> _logger;

    public SeedStateHealthCheck(MeepleAiDbContext db, ILogger<SeedStateHealthCheck> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>Public enum-as-strings so the health endpoint payload stays stable.</summary>
    public static class SeedStates
    {
        public const string Empty = "empty";
        public const string Indexing = "indexing";
        public const string PartialFailed = "partial_failed";
        public const string Ready = "ready";
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // #3075: exclude demo mock placeholders (seed/ prefix, no real blob/chunks) from the
            // RAG-readiness counts. SeedBadswormPersonaCommandHandler seeds them in deliberate
            // non-Ready states (Pending/Embedding) for the dashboard demo; they are not real corpus
            // content, so counting them would wrongly pin seed_state to indexing/partial_failed.
            var pdfTotal = await _db.PdfDocuments
                .AsNoTracking()
                .Where(d => !d.FilePath.StartsWith(PdfDocumentEntity.DemoMockFilePathPrefix))
                .CountAsync(cancellationToken)
                .ConfigureAwait(false);

            var pdfReady = await _db.PdfDocuments
                .AsNoTracking()
                .Where(d => !d.FilePath.StartsWith(PdfDocumentEntity.DemoMockFilePathPrefix))
                .CountAsync(d => d.ProcessingState == "Ready", cancellationToken)
                .ConfigureAwait(false);

            var pdfFailed = await _db.PdfDocuments
                .AsNoTracking()
                .Where(d => !d.FilePath.StartsWith(PdfDocumentEntity.DemoMockFilePathPrefix))
                .CountAsync(d => d.ProcessingState == "Failed", cancellationToken)
                .ConfigureAwait(false);

            var chunkCount = await _db.TextChunks
                .AsNoTracking()
                .CountAsync(cancellationToken)
                .ConfigureAwait(false);

            // VectorDocuments holds ONE row per PDF (an indexing-status record with a
            // ChunkCount), not one row per chunk. Counting rows would compare per-PDF
            // (≈ pdf_ready) against TextChunks' per-chunk count and never match for
            // multi-chunk PDFs, pinning seed_state to partial_failed forever (#2675).
            // Sum the per-PDF ChunkCount so both sides count chunks. Same idiom as
            // VectorDocumentRepository.GetTotalChunkCountAsync.
            var embeddingCount = await _db.VectorDocuments
                .AsNoTracking()
                .SumAsync(v => v.ChunkCount, cancellationToken)
                .ConfigureAwait(false);

            string seedState = DeriveSeedState(pdfTotal, pdfReady, pdfFailed, chunkCount, embeddingCount);

            var data = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["seed_state"] = seedState,
                ["pdf_total"] = pdfTotal,
                ["pdf_ready"] = pdfReady,
                ["pdf_failed"] = pdfFailed,
                ["chunk_count"] = chunkCount,
                ["embedding_count"] = embeddingCount,
            };

            // We never surface this as Unhealthy — even an empty DB is a valid state
            // for a freshly-restored stack. We use Degraded to flag attention without
            // breaking /health/ready (which only fails on Critical), so monitoring can
            // still alert when a partial bake degrades recall.
            return seedState switch
            {
                SeedStates.PartialFailed => HealthCheckResult.Degraded(
                    $"RAG partial-failed: chunks={chunkCount} embeddings={embeddingCount} failed_pdfs={pdfFailed}",
                    data: data),
                _ => HealthCheckResult.Healthy(
                    $"seed_state={seedState} (pdfs={pdfTotal}/{pdfReady}, chunks={chunkCount}, embeddings={embeddingCount})",
                    data: data),
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SeedStateHealthCheck failed to query DB counts");
            return HealthCheckResult.Unhealthy("Failed to query seed-state counts", ex);
        }
    }

    /// <summary>
    /// Pure function so we can unit-test the state machine without spinning up postgres.
    /// </summary>
    public static string DeriveSeedState(int pdfTotal, int pdfReady, int pdfFailed, int chunkCount, int embeddingCount)
    {
        if (pdfTotal == 0 && chunkCount == 0 && embeddingCount == 0)
        {
            return SeedStates.Empty;
        }

        // Bake failed mid-way: chunks and embeddings disagree, or a PDF ended in Failed.
        if (chunkCount != embeddingCount || pdfFailed > 0)
        {
            return SeedStates.PartialFailed;
        }

        // All PDFs are processed and counts agree → ready.
        if (pdfTotal > 0 && pdfTotal == pdfReady && chunkCount > 0 && embeddingCount > 0)
        {
            return SeedStates.Ready;
        }

        // PDFs exist but some are still pre-Ready (Pending/Uploading/.../Indexing),
        // or counts are zero on one side only — indexing is in flight.
        return SeedStates.Indexing;
    }
}
