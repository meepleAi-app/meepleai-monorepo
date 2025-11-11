using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Authentication.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of Session repository.
/// Maps between domain Session entity and UserSessionEntity persistence model.
/// </summary>
public class SessionRepository : ISessionRepository
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public SessionRepository(MeepleAiDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task<Session?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        var sessionEntity = await _dbContext.UserSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TokenHash == tokenHash, cancellationToken);

        return sessionEntity != null ? MapToDomain(sessionEntity) : null;
    }

    public async Task<List<Session>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var sessionEntities = await _dbContext.UserSessions
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        return sessionEntities.Select(MapToDomain).ToList();
    }

    public async Task<List<Session>> GetActiveSessionsByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var sessionEntities = await _dbContext.UserSessions
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.ExpiresAt > now && s.RevokedAt == null)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        return sessionEntities.Select(MapToDomain).ToList();
    }

    public async Task AddAsync(Session session, CancellationToken cancellationToken = default)
    {
        var sessionEntity = MapToPersistence(session);
        await _dbContext.UserSessions.AddAsync(sessionEntity, cancellationToken);
    }

    public async Task UpdateAsync(Session session, CancellationToken cancellationToken = default)
    {
        var sessionEntity = MapToPersistence(session);
        _dbContext.UserSessions.Update(sessionEntity);
        await Task.CompletedTask;
    }

    public async Task RevokeAllUserSessionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        await _dbContext.UserSessions
            .Where(s => s.UserId == userId && s.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now), cancellationToken);
    }

    /// <summary>
    /// Maps persistence entity to domain entity.
    /// </summary>
    private static Session MapToDomain(Api.Infrastructure.Entities.UserSessionEntity entity)
    {
        // Reconstruct session token from hash (we don't have the original token)
        var sessionToken = SessionToken.FromStored(Convert.ToBase64String(Guid.NewGuid().ToByteArray()));

        var session = new Session(
            id: entity.Id,
            userId: entity.UserId,
            token: sessionToken // Note: This is a dummy token since we only store hash
        );

        // Use reflection to set private properties from DB
        var tokenHashProp = typeof(Session).GetProperty("TokenHash");
        tokenHashProp?.SetValue(session, entity.TokenHash);

        var createdAtProp = typeof(Session).GetProperty("CreatedAt");
        createdAtProp?.SetValue(session, entity.CreatedAt);

        var expiresAtProp = typeof(Session).GetProperty("ExpiresAt");
        expiresAtProp?.SetValue(session, entity.ExpiresAt);

        var lastSeenAtProp = typeof(Session).GetProperty("LastSeenAt");
        lastSeenAtProp?.SetValue(session, entity.LastSeenAt);

        var revokedAtProp = typeof(Session).GetProperty("RevokedAt");
        revokedAtProp?.SetValue(session, entity.RevokedAt);

        var ipAddressProp = typeof(Session).GetProperty("IpAddress");
        ipAddressProp?.SetValue(session, entity.IpAddress);

        var userAgentProp = typeof(Session).GetProperty("UserAgent");
        userAgentProp?.SetValue(session, entity.UserAgent);

        return session;
    }

    /// <summary>
    /// Maps domain entity to persistence entity.
    /// </summary>
    private static Api.Infrastructure.Entities.UserSessionEntity MapToPersistence(Session domainEntity)
    {
        return new Api.Infrastructure.Entities.UserSessionEntity
        {
            Id = domainEntity.Id,
            UserId = domainEntity.UserId,
            TokenHash = domainEntity.TokenHash,
            CreatedAt = domainEntity.CreatedAt,
            ExpiresAt = domainEntity.ExpiresAt,
            LastSeenAt = domainEntity.LastSeenAt,
            RevokedAt = domainEntity.RevokedAt,
            IpAddress = domainEntity.IpAddress,
            UserAgent = domainEntity.UserAgent,
            User = null! // Required navigation property, will be loaded by EF Core
        };
    }
}
