using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Persistence;

internal sealed class GamebookCampaignSessionRepository : IGamebookCampaignSessionRepository
{
    private readonly MeepleAiDbContext _db;

    public GamebookCampaignSessionRepository(MeepleAiDbContext db) => _db = db;

    // #2734: the DbContext default is QueryTrackingBehavior.NoTracking (PERF-06). This getter
    // feeds RenameGamebookCampaignHandler (Rename()) and UpdateGamebookProgressHandler (Touch()),
    // which mutate the returned aggregate and rely on SaveChangesAsync to persist it. Without
    // .AsTracking() the entity is untracked and every scalar mutation is a silent no-op.
    // Tracking a single FirstOrDefault entity is negligible and side-effect-free for the
    // read-only callers (e.g. CampaignOwnershipGuard), which never mutate it.
    public Task<GamebookCampaignSession?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.GamebookCampaignSessions.AsTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<GamebookCampaignSession>> ListByOwnerAsync(Guid ownerUserId, Guid? gameId, CancellationToken ct = default)
    {
        var q = _db.GamebookCampaignSessions.Where(x => x.OwnerUserId == ownerUserId);
        // A0.2 (#1320): gameId filter still uses bare Guid at the wire level;
        // match on GameRef.Id regardless of Kind for backward compatibility.
        if (gameId.HasValue) q = q.Where(x => x.GameRef.Id == gameId.Value);
        return await q.OrderByDescending(x => x.UpdatedAt).ToListAsync(ct).ConfigureAwait(false);
    }

    public Task AddAsync(GamebookCampaignSession session, CancellationToken ct = default)
        => _db.GamebookCampaignSessions.AddAsync(session, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
