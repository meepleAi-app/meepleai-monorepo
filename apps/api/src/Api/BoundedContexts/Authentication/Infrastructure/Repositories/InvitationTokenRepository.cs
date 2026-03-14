using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Enums;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.Authentication;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Authentication.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for InvitationToken aggregate.
/// Handles mapping between domain and infrastructure entities.
/// </summary>
internal sealed class InvitationTokenRepository : IInvitationTokenRepository
{
    private readonly MeepleAiDbContext _context;

    public InvitationTokenRepository(MeepleAiDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public async Task<InvitationToken?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.InvitationTokens
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            .ConfigureAwait(false);
        return entity == null ? null : MapToDomain(entity);
    }

    public async Task<IReadOnlyList<InvitationToken>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _context.InvitationTokens
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return entities.Select(MapToDomain).ToList();
    }

    public async Task AddAsync(InvitationToken entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var infrastructureEntity = MapToInfrastructure(entity);
        await _context.InvitationTokens.AddAsync(infrastructureEntity, cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(InvitationToken entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var existingEntity = await _context.InvitationTokens
            .FindAsync(new object[] { entity.Id }, cancellationToken)
            .ConfigureAwait(false);

        if (existingEntity == null)
            throw new InvalidOperationException($"InvitationToken {entity.Id} not found");

        // Update mutable fields
        existingEntity.Status = entity.Status.ToString();
        existingEntity.AcceptedAt = entity.AcceptedAt;
        existingEntity.AcceptedByUserId = entity.AcceptedByUserId;
        existingEntity.RevokedAt = entity.RevokedAt;
    }

    public async Task DeleteAsync(InvitationToken entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var existingEntity = await _context.InvitationTokens
            .FindAsync(new object[] { entity.Id }, cancellationToken)
            .ConfigureAwait(false);

        if (existingEntity != null)
        {
            _context.InvitationTokens.Remove(existingEntity);
        }
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.InvitationTokens
            .AnyAsync(t => t.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<InvitationToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        var entity = await _context.InvitationTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken)
            .ConfigureAwait(false);
        return entity == null ? null : MapToDomain(entity);
    }

    public async Task<InvitationToken?> GetPendingByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var pendingStatus = InvitationStatus.Pending.ToString();
        var entity = await _context.InvitationTokens
            .FirstOrDefaultAsync(t => t.Email == normalizedEmail && t.Status == pendingStatus, cancellationToken)
            .ConfigureAwait(false);
        return entity == null ? null : MapToDomain(entity);
    }

    public async Task<IReadOnlyList<InvitationToken>> GetByStatusAsync(
        InvitationStatus? status, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.InvitationTokens.AsQueryable();
        if (status.HasValue)
        {
            var statusString = status.Value.ToString();
            query = query.Where(t => t.Status == statusString);
        }

        var entities = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<int> CountByStatusAsync(InvitationStatus status, CancellationToken cancellationToken = default)
    {
        var statusString = status.ToString();
        return await _context.InvitationTokens
            .CountAsync(t => t.Status == statusString, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> CountAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.InvitationTokens
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Maps infrastructure entity to domain aggregate.
    /// Uses internal factory + RestoreState method (no reflection).
    /// </summary>
    private static InvitationToken MapToDomain(InvitationTokenEntity entity)
    {
        var token = InvitationToken.CreateForHydration(entity.Id);

        // Restore all state via internal method
        token.RestoreState(
            email: entity.Email,
            role: entity.Role,
            tokenHash: entity.TokenHash,
            invitedByUserId: entity.InvitedByUserId,
            status: Enum.Parse<InvitationStatus>(entity.Status),
            expiresAt: entity.ExpiresAt,
            acceptedAt: entity.AcceptedAt,
            acceptedByUserId: entity.AcceptedByUserId,
            createdAt: entity.CreatedAt,
            revokedAt: entity.RevokedAt);

        return token;
    }

    /// <summary>
    /// Maps domain aggregate to infrastructure entity.
    /// </summary>
    private static InvitationTokenEntity MapToInfrastructure(InvitationToken domain)
    {
        return new InvitationTokenEntity
        {
            Id = domain.Id,
            Email = domain.Email,
            Role = domain.Role,
            TokenHash = domain.TokenHash,
            InvitedByUserId = domain.InvitedByUserId,
            Status = domain.Status.ToString(),
            ExpiresAt = domain.ExpiresAt,
            AcceptedAt = domain.AcceptedAt,
            AcceptedByUserId = domain.AcceptedByUserId,
            CreatedAt = domain.CreatedAt,
            RevokedAt = domain.RevokedAt
        };
    }
}
