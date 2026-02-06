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

    public async Task<IReadOnlyDictionary<Guid, ShareRequest>> GetByIdsForUpdateAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var idList = ids.ToList();
        if (idList.Count == 0)
        {
            return new Dictionary<Guid, ShareRequest>();
        }

        // Batch query with tracking enabled for updates
        var entities = await _context.Set<ShareRequestEntity>()
            .Include(e => e.AttachedDocuments)
            .Where(e => idList.Contains(e.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Map to dictionary for O(1) lookup by ID
        return entities.ToDictionary(
            e => e.Id,
            e => MapToDomain(e));
    }

    public void Update(ShareRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Find the tracked entity (loaded by GetByIdForUpdateAsync)
        var trackedEntity = _context.Set<ShareRequestEntity>()
            .Local
            .FirstOrDefault(e => e.Id == request.Id);

        if (trackedEntity != null)
        {
            // Update the tracked entity's properties directly
            trackedEntity.UserId = request.UserId;
            trackedEntity.SourceGameId = request.SourceGameId;
            trackedEntity.TargetSharedGameId = request.TargetSharedGameId;
            trackedEntity.Status = (int)request.Status;
            trackedEntity.StatusBeforeReview = request.StatusBeforeReview.HasValue ? (int)request.StatusBeforeReview.Value : null;
            trackedEntity.ContributionType = (int)request.ContributionType;
            trackedEntity.UserNotes = request.UserNotes;
            trackedEntity.AdminFeedback = request.AdminFeedback;
            trackedEntity.ReviewingAdminId = request.ReviewingAdminId;
            trackedEntity.ReviewStartedAt = request.ReviewStartedAt;
            trackedEntity.ReviewLockExpiresAt = request.ReviewLockExpiresAt;
            trackedEntity.ResolvedAt = request.ResolvedAt;
            trackedEntity.ModifiedAt = request.ModifiedAt;
            trackedEntity.ModifiedBy = request.ModifiedBy;
            // RowVersion is managed by EF Core concurrency token
        }
        else
        {
            // No tracked entity found - attach a new one
            var entity = MapToEntity(request);
            _context.Set<ShareRequestEntity>().Update(entity);
        }
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
            entity.SourcePrivateGameId,
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
            SourcePrivateGameId = request.SourcePrivateGameId,
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

    public async Task<int> CountApprovedByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<ShareRequestEntity>()
            .Where(r => r.UserId == userId && r.Status == (int)ShareRequestStatus.Approved)
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<List<ShareRequest>> GetRecentResolvedByUserAsync(
        Guid userId,
        int count,
        CancellationToken cancellationToken = default)
    {
        var entities = await _context.Set<ShareRequestEntity>()
            .Where(r => r.UserId == userId &&
                       (r.Status == (int)ShareRequestStatus.Approved ||
                        r.Status == (int)ShareRequestStatus.Rejected))
            .OrderByDescending(r => r.ModifiedAt)
            .Take(count)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    // ===== Rate Limiting Support Methods (Issue #2730) =====

    public async Task<int> CountPendingByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<ShareRequestEntity>()
            .Where(r => r.UserId == userId &&
                       (r.Status == (int)ShareRequestStatus.Pending ||
                        r.Status == (int)ShareRequestStatus.InReview ||
                        r.Status == (int)ShareRequestStatus.ChangesRequested))
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> CountThisMonthByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var firstDayOfMonth = new DateTime(
            DateTime.UtcNow.Year,
            DateTime.UtcNow.Month,
            1,
            0,
            0,
            0,
            DateTimeKind.Utc);

        return await _context.Set<ShareRequestEntity>()
            .Where(r => r.UserId == userId && r.CreatedAt >= firstDayOfMonth)
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<DateTime?> GetLastRejectionDateAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var lastRejection = await _context.Set<ShareRequestEntity>()
            .AsNoTracking()
            .Where(r => r.UserId == userId && r.Status == (int)ShareRequestStatus.Rejected)
            .OrderByDescending(r => r.ResolvedAt)
            .Select(r => r.ResolvedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return lastRejection;
    }

    // ===== Admin Dashboard Support Methods (Issue #2740) =====

    public async Task<int> CountPendingAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Set<ShareRequestEntity>()
            .AsNoTracking()
            .CountAsync(r => r.Status == (int)ShareRequestStatus.Pending, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> CountInReviewAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Set<ShareRequestEntity>()
            .AsNoTracking()
            .CountAsync(r => r.Status == (int)ShareRequestStatus.InReview, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<ShareRequest>> GetPendingOlderThanAsync(
        DateTime threshold,
        CancellationToken cancellationToken = default)
    {
        var entities = await _context.Set<ShareRequestEntity>()
            .AsNoTracking()
            .Include(e => e.AttachedDocuments)
            .Where(r => r.Status == (int)ShareRequestStatus.Pending && r.CreatedAt < threshold)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList().AsReadOnly();
    }

    public async Task<PendingShareRequestStats> GetPendingStatsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var todayStart = now.Date;

        var pendingRequests = await _context.Set<ShareRequestEntity>()
            .AsNoTracking()
            .Where(r => r.Status == (int)ShareRequestStatus.Pending)
            .Select(r => new { r.CreatedAt, r.ContributionType })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var totalPending = pendingRequests.Count;
        var createdToday = pendingRequests.Count(r => r.CreatedAt >= todayStart);
        var oldestPendingAge = totalPending > 0
            ? now - pendingRequests.Min(r => r.CreatedAt)
            : TimeSpan.Zero;

        var byType = pendingRequests
            .GroupBy(r => ((ContributionType)r.ContributionType).ToString(), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        return new PendingShareRequestStats
        {
            TotalPending = totalPending,
            CreatedToday = createdToday,
            OldestPendingAge = oldestPendingAge,
            ByType = byType
        };
    }

    /// <summary>
    /// Gets a pending game proposal for a private game.
    /// Issue #3665: Phase 4 - Proposal System.
    /// </summary>
    public async Task<ShareRequest?> GetPendingProposalForPrivateGameAsync(
        Guid privateGameId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.Set<ShareRequestEntity>()
            .AsNoTracking()
            .Include(e => e.AttachedDocuments)
            .Where(r => r.SourcePrivateGameId == privateGameId &&
                       r.ContributionType == (int)ContributionType.NewGameProposal &&
                       (r.Status == (int)ShareRequestStatus.Pending ||
                        r.Status == (int)ShareRequestStatus.InReview ||
                        r.Status == (int)ShareRequestStatus.ChangesRequested))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    #endregion
}
