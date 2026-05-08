using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Persistence;

internal sealed class GamebookCampaignSessionRepository : IGamebookCampaignSessionRepository
{
    private readonly MeepleAiDbContext _db;

    public GamebookCampaignSessionRepository(MeepleAiDbContext db) => _db = db;

    public Task<GamebookCampaignSession?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.GamebookCampaignSessions.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<GamebookCampaignSession>> ListByOwnerAsync(Guid ownerUserId, Guid? gameId, CancellationToken ct = default)
    {
        var q = _db.GamebookCampaignSessions.Where(x => x.OwnerUserId == ownerUserId);
        if (gameId.HasValue) q = q.Where(x => x.GameId == gameId.Value);
        return await q.OrderByDescending(x => x.UpdatedAt).ToListAsync(ct).ConfigureAwait(false);
    }

    public Task AddAsync(GamebookCampaignSession session, CancellationToken ct = default)
        => _db.GamebookCampaignSessions.AddAsync(session, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
