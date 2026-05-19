using Api.BoundedContexts.SessionTracking.Domain.Entities;

namespace Api.BoundedContexts.SessionTracking.Domain.Repositories;

public interface IGamebookGlossaryRepository
{
    Task<IReadOnlyList<GamebookGlossaryEntry>> ListByCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default);
    Task<GamebookGlossaryEntry?> GetByTermAsync(Guid campaignId, string termEn, CancellationToken cancellationToken = default);

    /// <summary>
    /// Issue #1312: cross-entry lookup by Italian translation, used to detect
    /// duplicate <c>termIt</c> values on the same campaign during upsert.
    /// Matching is case-insensitive and trim-tolerant. Returns null when no
    /// other entry uses the candidate term.
    /// </summary>
    Task<GamebookGlossaryEntry?> GetByTermItAsync(Guid campaignId, string termIt, CancellationToken cancellationToken = default);

    Task<GamebookGlossaryEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<GamebookGlossaryEntry> entries, CancellationToken cancellationToken = default);
    Task AddAsync(GamebookGlossaryEntry entry, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
