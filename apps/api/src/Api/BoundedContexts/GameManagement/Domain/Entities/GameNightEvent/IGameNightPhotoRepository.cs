using Api.Infrastructure.Entities.GameManagement;

namespace Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;

/// <summary>
/// Repository for GameNight recap photos (Issue #2724). POCO-based and tracked:
/// AddAsync/RemoveAsync enqueue on the change tracker without saving — the command
/// handler drives the UnitOfWork.SaveChangesAsync. Kept separate from the
/// GameNightEvent aggregate repository (detached full-remap Update cannot insert
/// new PK-set children).
/// </summary>
internal interface IGameNightPhotoRepository
{
    Task AddAsync(GameNightPhotoEntity photo, CancellationToken cancellationToken = default);

    Task RemoveAsync(GameNightPhotoEntity photo, CancellationToken cancellationToken = default);

    Task<GameNightPhotoEntity?> GetByIdAsync(Guid photoId, CancellationToken cancellationToken = default);

    /// <summary>Returns the existing photo with the same (gameNightId, sha256) for idempotent re-upload, else null.</summary>
    Task<GameNightPhotoEntity?> GetBySha256Async(Guid gameNightId, string sha256, CancellationToken cancellationToken = default);

    /// <summary>Lists a night's photos, oldest first.</summary>
    Task<IReadOnlyList<GameNightPhotoEntity>> GetByGameNightIdAsync(Guid gameNightId, CancellationToken cancellationToken = default);
}
