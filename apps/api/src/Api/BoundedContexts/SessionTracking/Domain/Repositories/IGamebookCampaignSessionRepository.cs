using Api.BoundedContexts.SessionTracking.Domain.Entities;

namespace Api.BoundedContexts.SessionTracking.Domain.Repositories;

public interface IGamebookCampaignSessionRepository
{
    Task<GamebookCampaignSession?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<GamebookCampaignSession>> ListByOwnerAsync(Guid ownerUserId, Guid? gameId, CancellationToken ct = default);
    Task AddAsync(GamebookCampaignSession session, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
