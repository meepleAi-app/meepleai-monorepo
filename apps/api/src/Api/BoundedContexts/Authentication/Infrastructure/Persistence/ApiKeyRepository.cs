using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Authentication.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of ApiKey repository.
/// Maps between domain ApiKey entity and ApiKeyEntity persistence model.
/// </summary>
public class ApiKeyRepository : RepositoryBase, IApiKeyRepository
{
    public ApiKeyRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<ApiKey?> GetByKeyPrefixAsync(string keyPrefix, CancellationToken cancellationToken = default)
    {
        var apiKeyEntity = await DbContext.ApiKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.KeyPrefix == keyPrefix, cancellationToken).ConfigureAwait(false);

        return apiKeyEntity != null ? MapToDomain(apiKeyEntity) : null;
    }

    public async Task<List<ApiKey>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var apiKeyEntities = await DbContext.ApiKeys
            .AsNoTracking()
            .Where(k => k.UserId == userId)
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return apiKeyEntities.Select(MapToDomain).ToList();
    }

    public async Task<List<ApiKey>> GetActiveKeysByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var apiKeyEntities = await DbContext.ApiKeys
            .AsNoTracking()
            .Where(k => k.UserId == userId &&
                       k.IsActive &&
                       k.RevokedAt == null &&
                       (k.ExpiresAt == null || k.ExpiresAt > now))
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return apiKeyEntities.Select(MapToDomain).ToList();
    }

    public async Task<List<ApiKey>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var apiKeyEntities = await DbContext.ApiKeys
            .AsNoTracking()
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return apiKeyEntities.Select(MapToDomain).ToList();
    }

    public async Task AddAsync(ApiKey apiKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(apiKey);
        // Collect domain events BEFORE mapping to persistence entity
        CollectDomainEvents(apiKey);

        var apiKeyEntity = MapToPersistence(apiKey);
        await DbContext.ApiKeys.AddAsync(apiKeyEntity, cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(ApiKey apiKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(apiKey);
        // Collect domain events BEFORE updating persistence entity
        CollectDomainEvents(apiKey);

        var apiKeyEntity = MapToPersistence(apiKey);
        DbContext.ApiKeys.Update(apiKeyEntity);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Maps persistence entity to domain entity.
    /// Note: We cannot fully reconstruct the ApiKey domain entity because it's created via static factory.
    /// This is a simplified mapping that uses reflection to set private properties.
    /// </summary>
    private static ApiKey MapToDomain(Api.Infrastructure.Entities.ApiKeyEntity entity)
    {
        // Create a dummy API key (we can't recreate the original plaintext key)
        var (apiKey, _) = ApiKey.Create(
            id: entity.Id,
            userId: entity.UserId,
            keyName: entity.KeyName,
            scopes: entity.Scopes,
            expiresAt: entity.ExpiresAt,
            metadata: entity.Metadata
        );

        // Override properties from DB using reflection
        var keyHashProp = typeof(ApiKey).GetProperty("KeyHash");
        keyHashProp?.SetValue(apiKey, entity.KeyHash);

        var keyPrefixProp = typeof(ApiKey).GetProperty("KeyPrefix");
        keyPrefixProp?.SetValue(apiKey, entity.KeyPrefix);

        var createdAtProp = typeof(ApiKey).GetProperty("CreatedAt");
        createdAtProp?.SetValue(apiKey, entity.CreatedAt);

        var lastUsedAtProp = typeof(ApiKey).GetProperty("LastUsedAt");
        lastUsedAtProp?.SetValue(apiKey, entity.LastUsedAt);

        var revokedAtProp = typeof(ApiKey).GetProperty("RevokedAt");
        revokedAtProp?.SetValue(apiKey, entity.RevokedAt);

        var revokedByProp = typeof(ApiKey).GetProperty("RevokedBy");
        revokedByProp?.SetValue(apiKey, entity.RevokedBy);

        var isActiveProp = typeof(ApiKey).GetProperty("IsActive");
        isActiveProp?.SetValue(apiKey, entity.IsActive);

        return apiKey;
    }

    /// <summary>
    /// Maps domain entity to persistence entity.
    /// </summary>
    private static Api.Infrastructure.Entities.ApiKeyEntity MapToPersistence(ApiKey domainEntity)
    {
        return new Api.Infrastructure.Entities.ApiKeyEntity
        {
            Id = domainEntity.Id,
            UserId = domainEntity.UserId,
            KeyName = domainEntity.KeyName,
            KeyHash = domainEntity.KeyHash,
            KeyPrefix = domainEntity.KeyPrefix,
            Scopes = domainEntity.Scopes,
            CreatedAt = domainEntity.CreatedAt,
            ExpiresAt = domainEntity.ExpiresAt,
            LastUsedAt = domainEntity.LastUsedAt,
            RevokedAt = domainEntity.RevokedAt,
            RevokedBy = domainEntity.RevokedBy,
            IsActive = domainEntity.IsActive,
            Metadata = domainEntity.Metadata,
            User = null! // Required navigation property, will be loaded by EF Core
        };
    }
}
