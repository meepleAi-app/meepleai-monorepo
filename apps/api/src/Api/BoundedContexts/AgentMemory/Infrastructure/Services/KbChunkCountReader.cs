using Api.BoundedContexts.AgentMemory.Application.Services;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.AgentMemory.Infrastructure.Services;

/// <summary>
/// Reads the count of indexed VectorDocuments for a game from the shared DbContext.
/// VectorDocuments are owned by the KnowledgeBase BC; this reader exposes a narrow
/// cross-BC contract so the AgentMemory ConnectionBar metadata aggregation (#2492)
/// does not depend on KnowledgeBase domain logic.
/// </summary>
internal sealed class KbChunkCountReader : IKbChunkCountReader
{
    private readonly MeepleAiDbContext _db;

    public KbChunkCountReader(MeepleAiDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public Task<int> CountByGameAsync(Guid gameId, CancellationToken ct = default)
    {
        if (gameId == Guid.Empty) return Task.FromResult(0);

        // VectorDocumentEntity carries both a legacy GameId and the canonical
        // SharedGameId (cross-BC ref to SharedGameCatalog). Match on either to
        // include all "official rules" chunks for the game regardless of origin.
        return _db.VectorDocuments
            .AsNoTracking()
            .CountAsync(
                v => v.SharedGameId == gameId || v.GameId == gameId,
                ct);
    }
}
