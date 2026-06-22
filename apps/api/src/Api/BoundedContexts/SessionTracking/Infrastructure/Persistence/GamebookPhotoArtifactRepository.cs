using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Persistence;

internal sealed class GamebookPhotoArtifactRepository : IGamebookPhotoArtifactRepository
{
    private readonly MeepleAiDbContext _db;

    public GamebookPhotoArtifactRepository(MeepleAiDbContext db) => _db = db;

    public Task<GamebookPhotoArtifact?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _db.GamebookPhotoArtifacts.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<GamebookPhotoArtifact>> ListExpiredAsync(DateTimeOffset asOf, CancellationToken cancellationToken = default)
    {
        return await _db.GamebookPhotoArtifacts
            .Where(x => x.ExpiresAt <= asOf)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public Task AddAsync(GamebookPhotoArtifact artifact, CancellationToken cancellationToken = default)
        => _db.GamebookPhotoArtifacts.AddAsync(artifact, cancellationToken).AsTask();

    public Task RemoveAsync(GamebookPhotoArtifact artifact, CancellationToken cancellationToken = default)
    {
        _db.GamebookPhotoArtifacts.Remove(artifact);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _db.SaveChangesAsync(cancellationToken);
}
