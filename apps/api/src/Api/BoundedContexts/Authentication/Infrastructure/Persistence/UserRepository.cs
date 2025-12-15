using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Authentication.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of User repository.
/// Maps between domain User entity and UserEntity persistence model.
/// </summary>
public class UserRepository : RepositoryBase, IUserRepository
{
    public UserRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Include OAuthAccounts for domain logic that requires checking auth methods
        var userEntity = await DbContext.Users
            .Include(u => u.BackupCodes)
            .Include(u => u.OAuthAccounts)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken).ConfigureAwait(false);

        return userEntity != null ? MapToDomain(userEntity) : null;
    }

    public async Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default)
    {
        // Include OAuthAccounts for domain logic that requires checking auth methods
        var userEntity = await DbContext.Users
            .Include(u => u.BackupCodes)
            .Include(u => u.OAuthAccounts)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email.Value, cancellationToken).ConfigureAwait(false);

        return userEntity != null ? MapToDomain(userEntity) : null;
    }

    public async Task<bool> ExistsByEmailAsync(Email email, CancellationToken cancellationToken = default)
    {
        return await DbContext.Users
            .AsNoTracking()
            .AnyAsync(u => u.Email == email.Value, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> HasAnyUsersAsync(CancellationToken cancellationToken = default)
    {
        return await DbContext.Users
            .AsNoTracking()
            .AnyAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        // Include OAuthAccounts for domain logic that requires checking auth methods
        var userEntities = await DbContext.Users
            .Include(u => u.BackupCodes)
            .Include(u => u.OAuthAccounts)
            .AsNoTracking()
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return userEntities.Select(MapToDomain).ToList();
    }

    public async Task AddAsync(User entity, CancellationToken cancellationToken = default)
    {
        // Collect domain events BEFORE mapping to persistence entity
        CollectDomainEvents(entity);

        var userEntity = new Api.Infrastructure.Entities.UserEntity
        {
            Id = entity.Id,
            Email = entity.Email.Value,
            DisplayName = entity.DisplayName,
            PasswordHash = entity.PasswordHash.Value,
            Role = entity.Role.Value,
            Tier = entity.Tier.Value,
            CreatedAt = entity.CreatedAt,
            TotpSecretEncrypted = entity.TotpSecret?.EncryptedValue,
            IsTwoFactorEnabled = entity.IsTwoFactorEnabled,
            TwoFactorEnabledAt = entity.TwoFactorEnabledAt,
            Language = entity.Language,
            EmailNotifications = entity.EmailNotifications,
            Theme = entity.Theme,
            DataRetentionDays = entity.DataRetentionDays
        };

        // Map backup codes
        foreach (var backupCode in entity.BackupCodes)
        {
            userEntity.BackupCodes.Add(new Api.Infrastructure.Entities.UserBackupCodeEntity
            {
                Id = Guid.NewGuid(),
                UserId = entity.Id,
                CodeHash = backupCode.HashedValue,
                IsUsed = backupCode.IsUsed,
                UsedAt = backupCode.UsedAt,
                CreatedAt = DateTime.UtcNow
            });
        }

        await DbContext.Users.AddAsync(userEntity, cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(User entity, CancellationToken cancellationToken = default)
    {
        // Collect domain events BEFORE updating persistence entity
        CollectDomainEvents(entity);

        // Load the existing user with backup codes for update
        // Note: We don't need to track OAuthAccounts here since they're managed via OAuthAccountRepository
        var existingUser = await DbContext.Users
            .Include(u => u.BackupCodes)
            .FirstOrDefaultAsync(u => u.Id == entity.Id, cancellationToken).ConfigureAwait(false);

        if (existingUser == null)
        {
            throw new InvalidOperationException($"User with ID {entity.Id} not found for update");
        }

        // Update scalar properties
        existingUser.Email = entity.Email.Value;
        existingUser.DisplayName = entity.DisplayName;
        existingUser.PasswordHash = entity.PasswordHash.Value;
        existingUser.Role = entity.Role.Value;
        existingUser.Tier = entity.Tier.Value;
        existingUser.TotpSecretEncrypted = entity.TotpSecret?.EncryptedValue;
        existingUser.IsTwoFactorEnabled = entity.IsTwoFactorEnabled;
        existingUser.TwoFactorEnabledAt = entity.TwoFactorEnabledAt;

        // Update preferences
        existingUser.Language = entity.Language;
        existingUser.EmailNotifications = entity.EmailNotifications;
        existingUser.Theme = entity.Theme;
        existingUser.DataRetentionDays = entity.DataRetentionDays;

        // Synchronize backup codes collection (delete old, add new)
        // This ensures we don't duplicate codes on every update
        existingUser.BackupCodes.Clear();

        foreach (var backupCode in entity.BackupCodes)
        {
            existingUser.BackupCodes.Add(new Api.Infrastructure.Entities.UserBackupCodeEntity
            {
                Id = Guid.NewGuid(),
                UserId = entity.Id,
                CodeHash = backupCode.HashedValue,
                IsUsed = backupCode.IsUsed,
                UsedAt = backupCode.UsedAt,
                CreatedAt = DateTime.UtcNow
            });
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    public async Task DeleteAsync(User entity, CancellationToken cancellationToken = default)
    {
        var userEntity = await DbContext.Users.FindAsync(new object[] { entity.Id }, cancellationToken).ConfigureAwait(false);
        if (userEntity != null)
        {
            DbContext.Users.Remove(userEntity);
        }
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Api.Infrastructure.Entities.UserBackupCodeEntity>> GetBackupCodesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await DbContext.UserBackupCodes
            .AsNoTracking()
            .Where(bc => bc.UserId == userId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Maps persistence entity to domain entity.
    /// </summary>
    private static User MapToDomain(Api.Infrastructure.Entities.UserEntity entity)
    {
        if (string.IsNullOrWhiteSpace(entity.Email))
        {
            throw new InvalidOperationException("Persisted user record is missing an Email value.");
        }

        if (string.IsNullOrWhiteSpace(entity.PasswordHash))
        {
            throw new InvalidOperationException($"Persisted user record {entity.Id} is missing a password hash.");
        }

        if (string.IsNullOrWhiteSpace(entity.Role))
        {
            throw new InvalidOperationException($"Persisted user record {entity.Id} is missing a role.");
        }

        var email = new Email(entity.Email);
        var passwordHash = PasswordHash.FromStored(entity.PasswordHash);
        var role = Role.Parse(entity.Role);
        var tier = !string.IsNullOrWhiteSpace(entity.Tier) ? UserTier.Parse(entity.Tier) : UserTier.Free;
        var displayName = string.IsNullOrWhiteSpace(entity.DisplayName)
            ? entity.Email
            : entity.DisplayName ?? entity.Email;

        var user = new User(
            id: entity.Id,
            email: email,
            displayName: displayName,
            passwordHash: passwordHash,
            role: role,
            tier: tier
        );

        // Reconstruct preferences (use domain method to apply)
        user.UpdatePreferences(
            entity.Language,
            entity.Theme,
            entity.EmailNotifications,
            entity.DataRetentionDays);

        // S3011 fix: Use internal hydration methods instead of reflection
        // Reconstruct 2FA state
        if (entity.IsTwoFactorEnabled && !string.IsNullOrEmpty(entity.TotpSecretEncrypted))
        {
            var totpSecret = TotpSecret.FromEncrypted(entity.TotpSecretEncrypted);

            // Convert backup codes from persistence to domain
            var backupCodes = entity.BackupCodes
                .Select(bc => BackupCode.FromHashed(bc.CodeHash, bc.IsUsed, bc.UsedAt))
                .ToList();

            // Use internal method instead of reflection (S3011 compliance)
            user.Restore2FAState(totpSecret, entity.IsTwoFactorEnabled, entity.TwoFactorEnabledAt, backupCodes);
        }

        // Reconstruct OAuth accounts collection
        if (entity.OAuthAccounts.Any())
        {
            var oauthAccounts = entity.OAuthAccounts.Select(oauthEntity => new OAuthAccount(
                oauthEntity.Id,
                oauthEntity.UserId,
                oauthEntity.Provider,
                oauthEntity.ProviderUserId,
                oauthEntity.AccessTokenEncrypted,
                oauthEntity.RefreshTokenEncrypted,
                oauthEntity.TokenExpiresAt
            )).ToList();

            // Use internal method instead of reflection (S3011 compliance)
            user.RestoreOAuthAccounts(oauthAccounts);
        }

        return user;
    }

    public async Task<int> CountAdminsAsync(CancellationToken cancellationToken = default)
    {
        var adminRole = Role.Admin.Value;
        return await DbContext.Users
            .AsNoTracking()
            .CountAsync(u => u.Role == adminRole, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<User>> SearchAsync(string query, int maxResults, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<User>();
        }

        // Include OAuthAccounts for domain logic that requires checking auth methods
        var userEntities = await DbContext.Users
            .Include(u => u.BackupCodes)
            .Include(u => u.OAuthAccounts)
            .Where(u => (u.DisplayName != null && u.DisplayName.Contains(query)) || u.Email.Contains(query))
            .OrderBy(u => u.DisplayName ?? u.Email)
            .Take(maxResults)
            .AsNoTracking()
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return userEntities.Select(MapToDomain).ToList();
    }
}
