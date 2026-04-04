using Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of ISessionAttachmentRepository.
/// Issue #5360 - SessionAttachment EF Core configuration + migration.
/// </summary>
internal sealed class SessionAttachmentRepository : RepositoryBase, ISessionAttachmentRepository
{

    public SessionAttachmentRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<SessionAttachment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.SessionAttachments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity == null ? null : MapToDomain(entity);
    }

    public async Task<IReadOnlyList<SessionAttachment>> GetBySessionIdAsync(
        Guid sessionId, CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.SessionAttachments
            .AsNoTracking()
            .Where(a => a.SessionId == sessionId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<SessionAttachment>> GetBySnapshotAsync(
        Guid sessionId, int snapshotIndex, CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.SessionAttachments
            .AsNoTracking()
            .Where(a => a.SessionId == sessionId && a.SnapshotIndex == snapshotIndex)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<int> CountByPlayerAndSnapshotAsync(
        Guid sessionId, Guid playerId, int? snapshotIndex, CancellationToken cancellationToken = default)
    {
        var query = DbContext.SessionAttachments
            .Where(a => a.SessionId == sessionId && a.PlayerId == playerId);

        if (snapshotIndex.HasValue)
            query = query.Where(a => a.SnapshotIndex == snapshotIndex.Value);

        return await query.CountAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddAsync(SessionAttachment attachment, CancellationToken cancellationToken = default)
    {
        var entity = MapToPersistence(attachment);
        await DbContext.SessionAttachments.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.SessionAttachments
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (entity == null)
            return false;

        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;
        return true;
    }

    public async Task<IReadOnlyList<SessionAttachment>> GetExpiredAttachmentsAsync(
        DateTime cutoffDate, int batchSize, CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.SessionAttachments
            .AsNoTracking()
            .Where(a => a.CreatedAt < cutoffDate)
            .OrderBy(a => a.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static SessionAttachment MapToDomain(SessionAttachmentEntity entity)
    {
        return SessionAttachment.Create(
            entity.SessionId,
            entity.PlayerId,
            (AttachmentType)entity.AttachmentType,
            entity.BlobUrl,
            entity.ContentType,
            entity.FileSizeBytes,
            entity.ThumbnailUrl,
            entity.Caption,
            entity.SnapshotIndex);
    }

    private static SessionAttachmentEntity MapToPersistence(SessionAttachment attachment)
    {
        return new SessionAttachmentEntity
        {
            Id = attachment.Id,
            SessionId = attachment.SessionId,
            SnapshotIndex = attachment.SnapshotIndex,
            PlayerId = attachment.PlayerId,
            AttachmentType = (int)attachment.AttachmentType,
            BlobUrl = attachment.BlobUrl,
            ThumbnailUrl = attachment.ThumbnailUrl,
            Caption = attachment.Caption,
            ContentType = attachment.ContentType,
            FileSizeBytes = attachment.FileSizeBytes,
            CreatedAt = attachment.CreatedAt,
            IsDeleted = attachment.IsDeleted,
            DeletedAt = attachment.DeletedAt,
        };
    }
}
