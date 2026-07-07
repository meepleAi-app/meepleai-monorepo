using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Infrastructure.Persistence;

/// <summary>
/// Tracked EF Core repository for GameNight recap photos (Issue #2724).
/// Does not call SaveChanges — the command handler owns the UnitOfWork boundary.
/// </summary>
internal sealed class GameNightPhotoRepository : IGameNightPhotoRepository
{
    private readonly MeepleAiDbContext _dbContext;

    public GameNightPhotoRepository(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task AddAsync(GameNightPhotoEntity photo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(photo);
        await _dbContext.GameNightPhotos.AddAsync(photo, cancellationToken).ConfigureAwait(false);
    }

    public Task RemoveAsync(GameNightPhotoEntity photo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(photo);
        _dbContext.GameNightPhotos.Remove(photo);
        return Task.CompletedTask;
    }

    public async Task<GameNightPhotoEntity?> GetByIdAsync(Guid photoId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.GameNightPhotos
            .FirstOrDefaultAsync(p => p.Id == photoId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<GameNightPhotoEntity?> GetBySha256Async(Guid gameNightId, string sha256, CancellationToken cancellationToken = default)
    {
        return await _dbContext.GameNightPhotos
            .FirstOrDefaultAsync(p => p.GameNightId == gameNightId && p.Sha256Hash == sha256, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<GameNightPhotoEntity>> GetByGameNightIdAsync(Guid gameNightId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.GameNightPhotos
            .Where(p => p.GameNightId == gameNightId)
            .OrderBy(p => p.UploadedAt)
            .ThenBy(p => p.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
