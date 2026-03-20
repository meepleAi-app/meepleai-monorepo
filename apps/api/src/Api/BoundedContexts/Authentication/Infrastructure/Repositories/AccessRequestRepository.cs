using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Enums;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.Authentication;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Authentication.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for AccessRequest aggregate.
/// Handles mapping between domain and infrastructure entities.
/// </summary>
internal sealed class AccessRequestRepository : RepositoryBase, IAccessRequestRepository
{
    public AccessRequestRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector) { }

    public async Task<AccessRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.AccessRequests
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<IReadOnlyList<AccessRequest>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.AccessRequests
            .OrderByDescending(e => e.RequestedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return entities.Select(MapToDomain).ToList();
    }

    public async Task AddAsync(AccessRequest entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        CollectDomainEvents(entity);
        var infrastructureEntity = MapToInfrastructure(entity);
        await DbContext.AccessRequests.AddAsync(infrastructureEntity, cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(AccessRequest entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        CollectDomainEvents(entity);
        var existing = await DbContext.AccessRequests
            .FindAsync(new object[] { entity.Id }, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
            throw new InvalidOperationException($"AccessRequest {entity.Id} not found");

        existing.Status = entity.Status.ToString();
        existing.ReviewedAt = entity.ReviewedAt;
        existing.ReviewedBy = entity.ReviewedBy;
        existing.RejectionReason = entity.RejectionReason;
        existing.InvitationId = entity.InvitationId;
    }

    public async Task DeleteAsync(AccessRequest entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var existing = await DbContext.AccessRequests
            .FindAsync(new object[] { entity.Id }, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
            DbContext.AccessRequests.Remove(existing);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.AccessRequests
            .AnyAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<AccessRequest?> GetPendingByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var pendingStatus = AccessRequestStatus.Pending.ToString();
        var entity = await DbContext.AccessRequests
            .FirstOrDefaultAsync(e => e.Email == normalizedEmail && e.Status == pendingStatus, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<IReadOnlyList<AccessRequest>> GetByStatusAsync(
        AccessRequestStatus? status, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = DbContext.AccessRequests.AsQueryable();
        if (status.HasValue)
        {
            var statusString = status.Value.ToString();
            query = query.Where(e => e.Status == statusString);
        }

        var entities = await query
            .OrderByDescending(e => e.RequestedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<int> CountByStatusAsync(AccessRequestStatus status, CancellationToken cancellationToken = default)
    {
        var statusString = status.ToString();
        return await DbContext.AccessRequests
            .CountAsync(e => e.Status == statusString, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> CountAllAsync(CancellationToken cancellationToken = default)
    {
        return await DbContext.AccessRequests
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Maps infrastructure entity to domain aggregate.
    /// Uses internal factory + RestoreState method (no reflection).
    /// </summary>
    private static AccessRequest MapToDomain(AccessRequestEntity entity)
    {
        var request = AccessRequest.CreateForHydration(entity.Id);

        request.RestoreState(
            email: entity.Email,
            status: Enum.Parse<AccessRequestStatus>(entity.Status),
            requestedAt: entity.RequestedAt,
            reviewedAt: entity.ReviewedAt,
            reviewedBy: entity.ReviewedBy,
            rejectionReason: entity.RejectionReason,
            invitationId: entity.InvitationId);

        return request;
    }

    /// <summary>
    /// Maps domain aggregate to infrastructure entity.
    /// </summary>
    private static AccessRequestEntity MapToInfrastructure(AccessRequest domain)
    {
        return new AccessRequestEntity
        {
            Id = domain.Id,
            Email = domain.Email,
            Status = domain.Status.ToString(),
            RequestedAt = domain.RequestedAt,
            ReviewedAt = domain.ReviewedAt,
            ReviewedBy = domain.ReviewedBy,
            RejectionReason = domain.RejectionReason,
            InvitationId = domain.InvitationId
        };
    }
}
