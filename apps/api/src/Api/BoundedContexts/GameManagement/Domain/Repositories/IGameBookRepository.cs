using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.GameManagement.Domain.Repositories;

public interface IGameBookRepository
{
    Task<GameBook?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<GameBook>> ListByGameRefAsync(GameRef gameRef, Guid? ownerUserId, CancellationToken ct);
    Task<GameBook?> FindCommunityByKbSourceAsync(Guid pdfDocId, CancellationToken ct);
    Task AddAsync(GameBook book, CancellationToken ct);
    Task UpdateAsync(GameBook book, CancellationToken ct);

    /// <summary>
    /// Issue #1388: batch lookup avoiding N+1 when callers need to resolve book
    /// display names for a known set of ids (e.g. campaign progress endpoint).
    /// Returns books in arbitrary order; callers must build a lookup dictionary.
    /// </summary>
    Task<IReadOnlyList<GameBook>> ListByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct);
}
