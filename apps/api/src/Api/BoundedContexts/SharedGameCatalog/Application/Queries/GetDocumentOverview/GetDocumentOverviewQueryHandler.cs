using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetDocumentOverview;

/// <summary>
/// Handler for GetDocumentOverviewQuery.
/// Performs cross-context read-only queries to build a consolidated document overview.
/// Issue #119: Per-SharedGame Document Overview.
/// </summary>
internal sealed class GetDocumentOverviewQueryHandler
    : IQueryHandler<GetDocumentOverviewQuery, DocumentOverviewResult>
{
    private readonly MeepleAiDbContext _db;
    private readonly ILogger<GetDocumentOverviewQueryHandler> _logger;

    public GetDocumentOverviewQueryHandler(
        MeepleAiDbContext db,
        ILogger<GetDocumentOverviewQueryHandler> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DocumentOverviewResult> Handle(
        GetDocumentOverviewQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Step 1: Get SharedGame title
        var game = await _db.SharedGames
            .AsNoTracking()
            .Where(g => g.Id == query.SharedGameId && !g.IsDeleted)
            .Select(g => new { g.Id, g.Title })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (game is null)
        {
            throw new Api.Middleware.Exceptions.NotFoundException("SharedGame", query.SharedGameId.ToString());
        }

        // Step 2: Get all PDF documents linked to this SharedGame
        // PDFs link to SharedGame via SharedGameId OR via GameEntity.SharedGameId bridge
        var pdfs = await _db.PdfDocuments
            .AsNoTracking()
            .Where(p => p.SharedGameId == query.SharedGameId
                || _db.Games.Any(g => g.SharedGameId == query.SharedGameId && g.Id == p.GameId))
            .Select(p => new
            {
                p.Id,
                p.FileName,
                p.DocumentCategory,
                p.DocumentType,
                p.ProcessingState,
                p.UploadedAt,
                p.IsActiveForRag,
            })
            .OrderByDescending(p => p.UploadedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Step 3: Get chunk counts from VectorDocuments
        var pdfIds = pdfs.Select(p => p.Id).ToList();
        var chunkCounts = pdfIds.Count > 0
            ? await _db.VectorDocuments
                .AsNoTracking()
                .Where(v => pdfIds.Contains(v.PdfDocumentId))
                .Select(v => new { v.PdfDocumentId, v.ChunkCount })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false)
            : [];

        var chunkMap = chunkCounts.ToDictionary(v => v.PdfDocumentId, v => v.ChunkCount);

        // Step 4: Calculate status breakdown
        var ready = pdfs.Count(p => string.Equals(p.ProcessingState, "Ready", StringComparison.Ordinal));
        var failed = pdfs.Count(p => string.Equals(p.ProcessingState, "Failed", StringComparison.Ordinal));
        var pending = pdfs.Count(p => string.Equals(p.ProcessingState, "Pending", StringComparison.Ordinal));
        var processing = pdfs.Count - ready - failed - pending;

        // Step 5: Map documents to DTOs
        var documents = pdfs.Select(p =>
        {
            var state = p.ProcessingState ?? "Unknown";
            string? currentStep = state is not ("Ready" or "Failed" or "Pending")
                ? state
                : null;

            return new DocumentOverviewItemDto(
                PdfDocumentId: p.Id,
                FileName: p.FileName,
                DocumentCategory: p.DocumentCategory,
                DocumentType: p.DocumentType,
                VersionLabel: null,
                ProcessingState: state,
                CurrentStep: currentStep,
                UploadedAt: p.UploadedAt,
                ChunkCount: chunkMap.GetValueOrDefault(p.Id),
                IsActiveForRag: p.IsActiveForRag
            );
        }).ToList();

        // Step 6: Agent system removed — no linked agents (Task 10: Agent cleanup)
        var agentStatus = new AgentStatusDto(
            HasLinkedAgent: false,
            AgentId: null,
            AgentName: null,
            IsActive: false,
            LastInvokedAt: null
        );

        // Step 7: Calculate RAG readiness
        var totalChunks = chunkMap.Values.Sum();
        var blockers = new List<string>();

        if (pdfs.Count == 0)
            blockers.Add("No documents uploaded");
        if (processing > 0)
            blockers.Add($"{processing} document(s) still processing");
        if (failed > 0)
            blockers.Add($"{failed} document(s) failed");
        // Agent system removed (Task 10: Agent cleanup) — agents no longer block RAG readiness

        var ragReadiness = new RagReadinessDto(
            IsReady: ready > 0 && failed == 0 && processing == 0,
            ReadyDocumentCount: ready,
            TotalChunks: totalChunks,
            Blockers: blockers
        );

        _logger.LogInformation(
            "DocumentOverview for SharedGame {Id}: {Total} docs ({Ready} ready, {Processing} processing, {Failed} failed)",
            query.SharedGameId, pdfs.Count, ready, processing, failed);

        return new DocumentOverviewResult(
            SharedGameId: game.Id,
            GameTitle: game.Title,
            TotalDocuments: pdfs.Count,
            StatusBreakdown: new StatusBreakdownDto(ready, processing, failed, pending),
            Documents: documents,
            AgentStatus: agentStatus,
            RagReadiness: ragReadiness
        );
    }
}
