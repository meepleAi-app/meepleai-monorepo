using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// DTO representing the KB status for a shared game.
/// Used by the frontend to determine if a shared game has indexed KB content
/// available for chat, without requiring the game to be in the user's library.
/// </summary>
internal sealed record SharedGameKbStatusDto(
    Guid GameId,
    bool HasKb,
    int IndexedCount,
    int ProcessingCount
);

/// <summary>
/// Query to retrieve KB indexing status for a shared game by its SharedGameId.
/// Checks PdfDocuments and VectorDocuments to determine if KB content is available.
/// </summary>
internal sealed record GetSharedGameKbStatusQuery(Guid SharedGameId)
    : IQuery<SharedGameKbStatusDto>;

/// <summary>
/// Handles GetSharedGameKbStatusQuery.
/// Queries PdfDocuments by SharedGameId to count indexed (Ready) and in-progress documents,
/// then checks VectorDocuments by SharedGameId for completed indexing.
/// </summary>
internal sealed class GetSharedGameKbStatusQueryHandler
    : IQueryHandler<GetSharedGameKbStatusQuery, SharedGameKbStatusDto>
{
    private readonly MeepleAiDbContext _db;

    public GetSharedGameKbStatusQueryHandler(MeepleAiDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<SharedGameKbStatusDto> Handle(
        GetSharedGameKbStatusQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Count VectorDocuments with completed indexing for this shared game
        var indexedCount = await _db.VectorDocuments
            .Where(v => v.SharedGameId == query.SharedGameId
                     && v.IndexingStatus == "completed")
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        // Count PdfDocuments currently being processed (not Ready, not Failed, not Pending)
        var processingCount = await _db.PdfDocuments
            .Where(p => p.SharedGameId == query.SharedGameId
                     && p.ProcessingState != "Ready"
                     && p.ProcessingState != "Failed"
                     && p.ProcessingState != "Pending")
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        return new SharedGameKbStatusDto(
            GameId: query.SharedGameId,
            HasKb: indexedCount > 0,
            IndexedCount: indexedCount,
            ProcessingCount: processingCount);
    }
}
