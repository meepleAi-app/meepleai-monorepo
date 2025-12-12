using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.Authentication;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Authentication.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for ShareLink aggregate.
/// Handles mapping between domain and infrastructure entities.
/// </summary>
public sealed class ShareLinkRepository : IShareLinkRepository
{
    private readonly MeepleAiDbContext _context;

    public ShareLinkRepository(MeepleAiDbContext context)
    {
        _context = context;
    }

    public async Task<ShareLink?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.ShareLinks.FindAsync(new object[] { id }, cancellationToken);
        return entity == null ? null : MapToDomain(entity);
    }

    public async Task<IReadOnlyList<ShareLink>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _context.ShareLinks.ToListAsync(cancellationToken);
        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<ShareLink>> FindByThreadIdAsync(Guid threadId, CancellationToken cancellationToken = default)
    {
        var entities = await _context.ShareLinks
            .Where(sl => sl.ThreadId == threadId)
            .ToListAsync(cancellationToken);
        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<ShareLink>> FindByCreatorIdAsync(Guid creatorId, CancellationToken cancellationToken = default)
    {
        var entities = await _context.ShareLinks
            .Where(sl => sl.CreatorId == creatorId)
            .ToListAsync(cancellationToken);
        return entities.Select(MapToDomain).ToList();
    }

    public async Task<ShareLink?> FindByThreadIdAndCreatorIdAsync(Guid threadId, Guid creatorId, CancellationToken cancellationToken = default)
    {
        var entity = await _context.ShareLinks
            .FirstOrDefaultAsync(
                sl => sl.ThreadId == threadId && sl.CreatorId == creatorId,
                cancellationToken);
        return entity == null ? null : MapToDomain(entity);
    }

    public async Task<IReadOnlyList<ShareLink>> FindActiveByThreadIdAsync(Guid threadId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var entities = await _context.ShareLinks
            .Where(sl => sl.ThreadId == threadId
                      && sl.RevokedAt == null
                      && sl.ExpiresAt > now)
            .ToListAsync(cancellationToken);
        return entities.Select(MapToDomain).ToList();
    }

    public async Task AddAsync(ShareLink entity, CancellationToken cancellationToken = default)
    {
        var infrastructureEntity = MapToInfrastructure(entity);
        await _context.ShareLinks.AddAsync(infrastructureEntity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(ShareLink entity, CancellationToken cancellationToken = default)
    {
        var existingEntity = await _context.ShareLinks.FindAsync(new object[] { entity.Id }, cancellationToken);
        if (existingEntity == null)
        {
            throw new InvalidOperationException($"ShareLink {entity.Id} not found");
        }

        // Update mutable fields
        existingEntity.RevokedAt = entity.IsRevoked ? entity.RevokedAt : null;
        existingEntity.ExpiresAt = entity.ExpiresAt;
        existingEntity.Label = entity.Label;
        existingEntity.AccessCount = entity.AccessCount;
        existingEntity.LastAccessedAt = entity.LastAccessedAt;

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(ShareLink entity, CancellationToken cancellationToken = default)
    {
        var existingEntity = await _context.ShareLinks.FindAsync(new object[] { entity.Id }, cancellationToken);
        if (existingEntity != null)
        {
            _context.ShareLinks.Remove(existingEntity);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.ShareLinks.AnyAsync(sl => sl.Id == id, cancellationToken);
    }

    /// <summary>
    /// Maps infrastructure entity to domain aggregate.
    /// </summary>
    private static ShareLink MapToDomain(ShareLinkEntity entity)
    {
        // Use reflection to reconstruct domain aggregate from infrastructure state
        var shareLink = (ShareLink)Activator.CreateInstance(typeof(ShareLink), true)!;

        typeof(ShareLink).GetProperty(nameof(ShareLink.Id))!.SetValue(shareLink, entity.Id);
        typeof(ShareLink).GetProperty(nameof(ShareLink.ThreadId))!.SetValue(shareLink, entity.ThreadId);
        typeof(ShareLink).GetProperty(nameof(ShareLink.CreatorId))!.SetValue(shareLink, entity.CreatorId);
        typeof(ShareLink).GetProperty(nameof(ShareLink.Role))!.SetValue(shareLink, entity.Role);
        typeof(ShareLink).GetProperty(nameof(ShareLink.ExpiresAt))!.SetValue(shareLink, entity.ExpiresAt);
        typeof(ShareLink).GetProperty(nameof(ShareLink.CreatedAt))!.SetValue(shareLink, entity.CreatedAt);
        typeof(ShareLink).GetProperty(nameof(ShareLink.RevokedAt))!.SetValue(shareLink, entity.RevokedAt);
        typeof(ShareLink).GetProperty(nameof(ShareLink.Label))!.SetValue(shareLink, entity.Label);
        typeof(ShareLink).GetProperty(nameof(ShareLink.AccessCount))!.SetValue(shareLink, entity.AccessCount);
        typeof(ShareLink).GetProperty(nameof(ShareLink.LastAccessedAt))!.SetValue(shareLink, entity.LastAccessedAt);

        return shareLink;
    }

    /// <summary>
    /// Maps domain aggregate to infrastructure entity.
    /// </summary>
    private static ShareLinkEntity MapToInfrastructure(ShareLink domain)
    {
        return new ShareLinkEntity
        {
            Id = domain.Id,
            ThreadId = domain.ThreadId,
            CreatorId = domain.CreatorId,
            Role = domain.Role,
            ExpiresAt = domain.ExpiresAt,
            CreatedAt = domain.CreatedAt,
            RevokedAt = domain.RevokedAt,
            Label = domain.Label,
            AccessCount = domain.AccessCount,
            LastAccessedAt = domain.LastAccessedAt
        };
    }
}
