using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Authentication.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of OAuth account repository.
/// Maps between domain OAuthAccount entity and OAuthAccountEntity persistence model.
/// </summary>
public class OAuthAccountRepository : RepositoryBase, IOAuthAccountRepository
{
    private readonly ILogger<OAuthAccountRepository> _logger;

    public OAuthAccountRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector, ILogger<OAuthAccountRepository> logger)
        : base(dbContext, eventCollector)
    {
        _logger = logger;
    }

    public async Task<OAuthAccount?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.OAuthAccounts
            .AsNoTracking()
            .Include(o => o.User)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken).ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<OAuthAccount?> GetByUserIdAndProviderAsync(Guid userId, string provider, CancellationToken cancellationToken = default)
    {
        var normalizedProvider = provider.ToLowerInvariant();
        var entity = await DbContext.OAuthAccounts
            .AsNoTracking()
            .Include(o => o.User)
            .FirstOrDefaultAsync(o => o.UserId == userId && o.Provider == normalizedProvider, cancellationToken).ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<IReadOnlyList<OAuthAccount>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.OAuthAccounts
            .AsNoTracking()
            .Include(o => o.User)
            .Where(o => o.UserId == userId)
            .OrderBy(o => o.Provider)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<OAuthAccount?> GetByProviderUserIdAsync(string provider, string providerUserId, CancellationToken cancellationToken = default)
    {
        var normalizedProvider = provider.ToLowerInvariant();
        var entity = await DbContext.OAuthAccounts
            .AsNoTracking()
            .Include(o => o.User)
            .FirstOrDefaultAsync(o => o.Provider == normalizedProvider && o.ProviderUserId == providerUserId, cancellationToken).ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<List<OAuthAccount>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.OAuthAccounts
            .AsNoTracking()
            .Include(o => o.User)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task AddAsync(OAuthAccount entity, CancellationToken cancellationToken = default)
    {
        // Collect domain events BEFORE mapping to persistence entity
        CollectDomainEvents(entity);

        var oauthEntity = MapToPersistence(entity);
        await DbContext.OAuthAccounts.AddAsync(oauthEntity, cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(OAuthAccount entity, CancellationToken cancellationToken = default)
    {
        // Collect domain events BEFORE updating persistence entity
        CollectDomainEvents(entity);

        var oauthEntity = MapToPersistence(entity);
        DbContext.OAuthAccounts.Update(oauthEntity);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    public async Task DeleteAsync(OAuthAccount entity, CancellationToken cancellationToken = default)
    {
        var oauthEntity = await DbContext.OAuthAccounts.FindAsync(new object[] { entity.Id }, cancellationToken).ConfigureAwait(false);
        if (oauthEntity != null)
        {
            DbContext.OAuthAccounts.Remove(oauthEntity);
        }
        else
        {
            _logger.LogWarning("Attempted to delete non-existent OAuth account with ID: {OAuthAccountId}", entity.Id);
        }
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.OAuthAccounts
            .AsNoTracking()
            .AnyAsync(o => o.Id == id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> ExistsByUserIdAndProviderAsync(Guid userId, string provider, CancellationToken cancellationToken = default)
    {
        var normalizedProvider = provider.ToLowerInvariant();
        return await DbContext.OAuthAccounts
            .AsNoTracking()
            .AnyAsync(o => o.UserId == userId && o.Provider == normalizedProvider, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Maps persistence entity to domain entity.
    /// </summary>
    private static OAuthAccount MapToDomain(OAuthAccountEntity entity)
    {
        if (string.IsNullOrWhiteSpace(entity.Provider))
        {
            throw new InvalidOperationException($"Persisted OAuth account {entity.Id} is missing a provider.");
        }

        if (string.IsNullOrWhiteSpace(entity.ProviderUserId))
        {
            throw new InvalidOperationException($"Persisted OAuth account {entity.Id} is missing a provider user ID.");
        }

        if (string.IsNullOrWhiteSpace(entity.AccessTokenEncrypted))
        {
            throw new InvalidOperationException($"Persisted OAuth account {entity.Id} is missing an access token.");
        }

        var account = new OAuthAccount(
            id: entity.Id,
            userId: entity.UserId,
            provider: entity.Provider,
            providerUserId: entity.ProviderUserId,
            accessTokenEncrypted: entity.AccessTokenEncrypted,
            refreshTokenEncrypted: entity.RefreshTokenEncrypted,
            tokenExpiresAt: entity.TokenExpiresAt
        );

        // Use reflection to set CreatedAt and UpdatedAt (they're set in constructor but we need persistence values)
        var createdAtProperty = typeof(OAuthAccount).GetProperty(nameof(OAuthAccount.CreatedAt));
        var updatedAtProperty = typeof(OAuthAccount).GetProperty(nameof(OAuthAccount.UpdatedAt));

        createdAtProperty?.SetValue(account, entity.CreatedAt);
        updatedAtProperty?.SetValue(account, entity.UpdatedAt);

        return account;
    }

    /// <summary>
    /// Maps domain entity to persistence entity.
    /// </summary>
    private static OAuthAccountEntity MapToPersistence(OAuthAccount domainEntity)
    {
        // Note: Tokens are already encrypted when passed from domain
        return new OAuthAccountEntity
        {
            Id = domainEntity.Id,
            UserId = domainEntity.UserId,
            Provider = domainEntity.Provider,
            ProviderUserId = domainEntity.ProviderUserId,
            AccessTokenEncrypted = domainEntity.AccessTokenEncrypted,
            RefreshTokenEncrypted = domainEntity.RefreshTokenEncrypted,
            TokenExpiresAt = domainEntity.TokenExpiresAt,
            CreatedAt = domainEntity.CreatedAt,
            UpdatedAt = domainEntity.UpdatedAt,
            User = null! // Navigation property, not needed for persistence
        };
    }
}
