using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;

/// <summary>
/// Repository for SharedGameDeleteRequest entity.
/// </summary>
internal sealed class SharedGameDeleteRequestRepository : ISharedGameDeleteRequestRepository
{
    private readonly MeepleAiDbContext _context;

    public SharedGameDeleteRequestRepository(MeepleAiDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task AddAsync(SharedGameDeleteRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var entity = new SharedGameDeleteRequestEntity
        {
            Id = request.Id,
            SharedGameId = request.SharedGameId,
            RequestedBy = request.RequestedBy,
            Reason = request.Reason,
            Status = (int)request.Status,
            ReviewedBy = request.ReviewedBy,
            ReviewComment = request.ReviewComment,
            CreatedAt = request.CreatedAt,
            ReviewedAt = request.ReviewedAt
        };

        await _context.SharedGameDeleteRequests.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public async Task<SharedGameDeleteRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.SharedGameDeleteRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public void Update(SharedGameDeleteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var entity = new SharedGameDeleteRequestEntity
        {
            Id = request.Id,
            SharedGameId = request.SharedGameId,
            RequestedBy = request.RequestedBy,
            Reason = request.Reason,
            Status = (int)request.Status,
            ReviewedBy = request.ReviewedBy,
            ReviewComment = request.ReviewComment,
            CreatedAt = request.CreatedAt,
            ReviewedAt = request.ReviewedAt
        };

        _context.SharedGameDeleteRequests.Update(entity);
    }

    private static SharedGameDeleteRequest MapToDomain(SharedGameDeleteRequestEntity entity)
    {
        return new SharedGameDeleteRequest(
            entity.Id,
            entity.SharedGameId,
            entity.RequestedBy,
            entity.Reason,
            (DeleteRequestStatus)entity.Status,
            entity.ReviewedBy,
            entity.ReviewComment,
            entity.CreatedAt,
            entity.ReviewedAt);
    }
}
