using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Persistence;

internal class SessionBookProgressRepository : ISessionBookProgressRepository
{
    private readonly MeepleAiDbContext _db;

    public SessionBookProgressRepository(MeepleAiDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public Task<SessionBookProgress?> GetByCampaignAndBookAsync(Guid campaignSessionId, Guid gameBookId, CancellationToken cancellationToken)
        => _db.SessionBookProgresses
            .FirstOrDefaultAsync(p => p.CampaignSessionId == campaignSessionId && p.GameBookId == gameBookId, cancellationToken);

    public async Task<IReadOnlyList<SessionBookProgress>> ListByCampaignAsync(Guid campaignSessionId, CancellationToken cancellationToken)
        => await _db.SessionBookProgresses
            .Where(p => p.CampaignSessionId == campaignSessionId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<SessionBookProgress?> GetMostRecentByCampaignAsync(Guid campaignSessionId, CancellationToken cancellationToken)
        => _db.SessionBookProgresses
            .Where(p => p.CampaignSessionId == campaignSessionId)
            .OrderByDescending(p => p.LastVisitedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task AddAsync(SessionBookProgress progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(progress);
        await _db.SessionBookProgresses.AddAsync(progress, cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(SessionBookProgress progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(progress);
        _db.SessionBookProgresses.Update(progress);
        return Task.CompletedTask;
    }

    public async Task DeleteByCampaignAsync(Guid campaignSessionId, CancellationToken cancellationToken)
    {
        var rows = await _db.SessionBookProgresses
            .Where(p => p.CampaignSessionId == campaignSessionId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (rows.Count == 0) return;
        _db.SessionBookProgresses.RemoveRange(rows);
    }
}
