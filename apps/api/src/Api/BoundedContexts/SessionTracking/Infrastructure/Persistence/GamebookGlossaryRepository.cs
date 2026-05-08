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
