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
}
