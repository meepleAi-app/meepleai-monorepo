using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Persistence;

internal sealed class GamebookGlossaryRepository : IGamebookGlossaryRepository
{
    private readonly MeepleAiDbContext _db;

    public GamebookGlossaryRepository(MeepleAiDbContext db) => _db = db;

    public async Task<IReadOnlyList<GamebookGlossaryEntry>> ListByCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default)
    {
        return await _db.GamebookGlossaryEntries
            .Where(x => x.CampaignId == campaignId)
            .OrderBy(x => x.TermEn)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<GamebookGlossaryEntry?> GetByTermAsync(Guid campaignId, string termEn, CancellationToken cancellationToken = default)
        => _db.GamebookGlossaryEntries
            .FirstOrDefaultAsync(x => x.CampaignId == campaignId && x.TermEn == termEn, cancellationToken);

    public Task<GamebookGlossaryEntry?> GetByTermItAsync(Guid campaignId, string termIt, CancellationToken cancellationToken = default)
    {
        // Issue #1312: case-insensitive + whitespace-tolerant cross-entry lookup.
        // Postgres-side comparison via ILIKE keeps the predicate translatable
        // without an extra round trip; the caller is responsible for passing
        // the user-supplied raw value (we trim + lowercase inside this method).
        var needle = (termIt ?? string.Empty).Trim();
        return _db.GamebookGlossaryEntries
            .FirstOrDefaultAsync(
                x => x.CampaignId == campaignId
                     && EF.Functions.ILike(x.TermIt.Trim(), needle),
                cancellationToken);
    }

    public Task<GamebookGlossaryEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _db.GamebookGlossaryEntries.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task AddRangeAsync(IEnumerable<GamebookGlossaryEntry> entries, CancellationToken cancellationToken = default)
    {
        await _db.GamebookGlossaryEntries.AddRangeAsync(entries, cancellationToken).ConfigureAwait(false);
    }

    public Task AddAsync(GamebookGlossaryEntry entry, CancellationToken cancellationToken = default)
        => _db.GamebookGlossaryEntries.AddAsync(entry, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _db.SaveChangesAsync(cancellationToken);
}
