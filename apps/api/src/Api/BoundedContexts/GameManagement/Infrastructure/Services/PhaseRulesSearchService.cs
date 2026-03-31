using Api.BoundedContexts.GameManagement.Domain.Services;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Infrastructure.Services;

/// <summary>
/// Searches text chunks for phase/turn-related rules using EF + PostgreSQL full-text search.
/// Uses the shared TextChunks table, filtered by gameId.
/// </summary>
internal sealed class PhaseRulesSearchService : IPhaseRulesSearchService
{
    private readonly MeepleAiDbContext _db;

    public PhaseRulesSearchService(MeepleAiDbContext db) =>
        _db = db ?? throw new ArgumentNullException(nameof(db));

    public async Task<IReadOnlyList<string>> SearchRulesChunksAsync(
        Guid gameId, string query, int topK, CancellationToken ct = default)
    {
        // Simple keyword search on Content — sufficient for phase suggestion
        var keywords = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var chunks = await _db.TextChunks
            .Where(c => c.GameId == gameId)
            .Where(c => keywords.Any(k => c.Content.Contains(k)))
            .OrderBy(c => c.ChunkIndex)
            .Take(topK)
            .Select(c => c.Content)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return chunks.AsReadOnly();
    }
}
