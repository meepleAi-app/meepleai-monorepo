using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for ShareRequest aggregate.
/// Issue #2724: CreateShareRequest Command infrastructure.
/// </summary>
internal sealed class ShareRequestRepository : IShareRequestRepository
{
    private readonly MeepleAiDbContext _context;

    public ShareRequestRepository(MeepleAiDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task AddAsync(ShareRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var entity = MapToEntity(request);
        await _context.Set<ShareRequestEntity>().AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ShareRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Set<ShareRequestEntity>()
            .AsNoTracking()
            .Include(e => e.AttachedDocuments)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<ShareRequest?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Set<ShareRequestEntity>()
            .Include(e => e.AttachedDocuments)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    public void Update(ShareRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var entity = MapToEntity(request);
        _context.Set<ShareRequestEntity>().Update(entity);
    }

    public async Task<bool> HasPendingRequestAsync(
        Guid userId,
        Guid sourceGameId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Set<ShareRequestEntity>()
            .AsNoTracking()
            .AnyAsync(e =>
                e.UserId == userId &&
                e.SourceGameId == sourceGameId &&
                (e.Status == (int)ShareRequestStatus.Pending ||
                 e.Status == (int)ShareRequestStatus.InReview ||
                 e.Status == (int)ShareRequestStatus.ChangesRequested),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<ShareRequest>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var entities = await _context.Set<ShareRequestEntity>()
            .AsNoTracking()
            .Include(e => e.AttachedDocuments)
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList().AsReadOnly();
    }

    public async Task<IReadOnlyCollection<ShareRequest>> GetByStatusAsync(
        ShareRequestStatus status,
        CancellationToken cancellationToken = default)
    {
        var entities = await _context.Set<ShareRequestEntity>()
            .AsNoTracking()
            .Include(e => e.AttachedDocuments)
            .Where(e => e.Status == (int)status)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList().AsReadOnly();
    }

    public async Task<IReadOnlyCollection<ShareRequest>> GetByReviewingAdminAsync(
        Guid adminId,
        CancellationToken cancellationToken = default)
    {
        var entities = await _context.Set<ShareRequestEntity>()
            .AsNoTracking()
            .Include(e => e.AttachedDocuments)
            .Where(e => e.ReviewingAdminId == adminId)
            .OrderByDescending(e => e.ReviewStartedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList().AsReadOnly();
    }

    public async Task<IReadOnlyCollection<ShareRequest>> GetStaleReviewsAsync(
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        var expirationThreshold = DateTime.UtcNow - duration;

        var entities = await _context.Set<ShareRequestEntity>()
            .AsNoTracking()
            .Include(e => e.AttachedDocuments)
            .Where(e =>
                e.Status == (int)ShareRequestStatus.InReview &&
                e.ReviewLockExpiresAt != null &&
                e.ReviewLockExpiresAt <= expirationThreshold)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList().AsReadOnly();
    }

    #region Mapping Methods

    private static ShareRequest MapToDomain(ShareRequestEntity entity)
    {
        var documents = entity.AttachedDocuments
            .Select(d => new ShareRequestDocument(
                d.Id,
                d.ShareRequestId,
                d.DocumentId,
                d.FileName,
                d.ContentType,
                d.FileSize,
                d.AttachedAt))
            .ToList();

        return new ShareRequest(
            entity.Id,
            entity.UserId,
            entity.SourceGameId,
            entity.TargetSharedGameId,
            (ShareRequestStatus)entity.Status,
            entity.StatusBeforeReview.HasValue ? (ShareRequestStatus)entity.StatusBeforeReview.Value : null,
            (ContributionType)entity.ContributionType,
            entity.UserNotes,
            entity.AdminFeedback,
            entity.ReviewingAdminId,
            entity.ReviewStartedAt,
            entity.ReviewLockExpiresAt,
            entity.ResolvedAt,
            entity.CreatedAt,
            entity.ModifiedAt,
            entity.CreatedBy,
            entity.ModifiedBy,
            entity.RowVersion,
            documents);
    }

    private static ShareRequestEntity MapToEntity(ShareRequest request)
    {
        var entity = new ShareRequestEntity
        {
            Id = request.Id,
            UserId = request.UserId,
            SourceGameId = request.SourceGameId,
            TargetSharedGameId = request.TargetSharedGameId,
            Status = (int)request.Status,
            StatusBeforeReview = request.StatusBeforeReview.HasValue ? (int)request.StatusBeforeReview.Value : null,
            ContributionType = (int)request.ContributionType,
            UserNotes = request.UserNotes,
            AdminFeedback = request.AdminFeedback,
            ReviewingAdminId = request.ReviewingAdminId,
            ReviewStartedAt = request.ReviewStartedAt,
            ReviewLockExpiresAt = request.ReviewLockExpiresAt,
            ResolvedAt = request.ResolvedAt,
            CreatedAt = request.CreatedAt,
            ModifiedAt = request.ModifiedAt,
            CreatedBy = request.CreatedBy,
            ModifiedBy = request.ModifiedBy,
            RowVersion = request.RowVersion
        };

        foreach (var doc in request.AttachedDocuments)
        {
            entity.AttachedDocuments.Add(new ShareRequestDocumentEntity
            {
                Id = doc.Id,
                ShareRequestId = request.Id,
                DocumentId = doc.DocumentId,
                FileName = doc.FileName,
                ContentType = doc.ContentType,
                FileSize = doc.FileSize,
                AttachedAt = doc.AttachedAt
            });
        }

        return entity;
    }

    #endregion
}
