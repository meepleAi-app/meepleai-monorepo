using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;

/// <summary>
/// Repository implementation for ChunkedUploadSession aggregate.
/// </summary>
internal class ChunkedUploadSessionRepository : RepositoryBase, IChunkedUploadSessionRepository
{
    public ChunkedUploadSessionRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<ChunkedUploadSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.ChunkedUploadSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken).ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<IReadOnlyList<ChunkedUploadSession>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.ChunkedUploadSessions
            .AsNoTracking()
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<ChunkedUploadSession>> FindActiveByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.ChunkedUploadSessions
            .AsNoTracking()
            .Where(s => s.UserId == userId &&
                        s.Status != "completed" &&
                        s.Status != "failed" &&
                        s.Status != "expired" &&
                        s.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<ChunkedUploadSession>> FindExpiredSessionsAsync(
        CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.ChunkedUploadSessions
            .AsNoTracking()
            .Where(s => s.ExpiresAt < DateTime.UtcNow &&
                        s.Status != "completed" &&
                        s.Status != "failed" &&
                        s.Status != "expired")
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<ChunkedUploadSession>> FindByStatusAsync(
        string status,
        CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.ChunkedUploadSessions
            .AsNoTracking()
            .Where(s => s.Status == status)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task AddAsync(ChunkedUploadSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        CollectDomainEvents(session);
        var entity = MapToPersistence(session);
        await DbContext.ChunkedUploadSessions.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(ChunkedUploadSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        CollectDomainEvents(session);
        var entity = MapToPersistence(session);

        // Detach existing tracked entity to avoid conflicts
        var tracked = DbContext.ChangeTracker.Entries<ChunkedUploadSessionEntity>()
            .FirstOrDefault(e => e.Entity.Id == entity.Id);

        if (tracked != null)
            tracked.State = EntityState.Detached;

        DbContext.ChunkedUploadSessions.Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(ChunkedUploadSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        var entity = MapToPersistence(session);
        DbContext.ChunkedUploadSessions.Remove(entity);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.ChunkedUploadSessions
            .AnyAsync(s => s.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    private static ChunkedUploadSession MapToDomain(ChunkedUploadSessionEntity entity)
    {
        var session = new ChunkedUploadSession(
            id: entity.Id,
            gameId: entity.GameId,
            userId: entity.UserId,
            fileName: entity.FileName,
            totalFileSize: entity.TotalFileSize,
            tempDirectory: entity.TempDirectory,
            privateGameId: entity.PrivateGameId
        );

        // Override properties from DB using reflection
        SetProperty(session, "ReceivedChunks", entity.ReceivedChunks);
        SetProperty(session, "ReceivedChunkIndices", entity.ReceivedChunkIndices);
        SetProperty(session, "Status", entity.Status);
        SetProperty(session, "CreatedAt", entity.CreatedAt);
        SetProperty(session, "CompletedAt", entity.CompletedAt);
        SetProperty(session, "ExpiresAt", entity.ExpiresAt);
        SetProperty(session, "ErrorMessage", entity.ErrorMessage);

        return session;
    }

    private static void SetProperty<T>(ChunkedUploadSession session, string propertyName, T value)
    {
        var prop = typeof(ChunkedUploadSession).GetProperty(propertyName);
        prop?.SetValue(session, value);
    }

    private static ChunkedUploadSessionEntity MapToPersistence(ChunkedUploadSession domain)
    {
        ArgumentNullException.ThrowIfNull(domain);
        return new ChunkedUploadSessionEntity
        {
            Id = domain.Id,
            GameId = domain.GameId,
            PrivateGameId = domain.PrivateGameId,
            UserId = domain.UserId,
            FileName = domain.FileName,
            TotalFileSize = domain.TotalFileSize,
            TotalChunks = domain.TotalChunks,
            ReceivedChunks = domain.ReceivedChunks,
            TempDirectory = domain.TempDirectory,
            Status = domain.Status,
            CreatedAt = domain.CreatedAt,
            CompletedAt = domain.CompletedAt,
            ExpiresAt = domain.ExpiresAt,
            ErrorMessage = domain.ErrorMessage,
            ReceivedChunkIndices = domain.ReceivedChunkIndices
        };
    }
}
