using Api.BoundedContexts.SessionTracking.Domain.Entities;

namespace Api.BoundedContexts.SessionTracking.Domain.Repositories;

public interface IGamebookGlossaryRepository
{
    Task<IReadOnlyList<GamebookGlossaryEntry>> ListByCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default);
    Task<GamebookGlossaryEntry?> GetByTermAsync(Guid campaignId, string termEn, CancellationToken cancellationToken = default);
    Task<GamebookGlossaryEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<GamebookGlossaryEntry> entries, CancellationToken cancellationToken = default);
    Task AddAsync(GamebookGlossaryEntry entry, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
