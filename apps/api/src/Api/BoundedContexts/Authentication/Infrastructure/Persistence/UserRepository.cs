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
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        return userEntity != null ? MapToDomain(userEntity) : null;
    }

    public async Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default)
    {
        // Include OAuthAccounts for domain logic that requires checking auth methods
        var userEntity = await DbContext.Users
            .Include(u => u.BackupCodes)
            .Include(u => u.OAuthAccounts)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email.Value, cancellationToken);

        return userEntity != null ? MapToDomain(userEntity) : null;
    }

    public async Task<bool> ExistsByEmailAsync(Email email, CancellationToken cancellationToken = default)
    {
        return await DbContext.Users
            .AsNoTracking()
            .AnyAsync(u => u.Email == email.Value, cancellationToken);
    }

    public async Task<bool> HasAnyUsersAsync(CancellationToken cancellationToken = default)
    {
        return await DbContext.Users
            .AsNoTracking()
            .AnyAsync(cancellationToken);
    }

    public async Task<List<User>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        // Include OAuthAccounts for domain logic that requires checking auth methods
        var userEntities = await DbContext.Users
            .Include(u => u.BackupCodes)
            .Include(u => u.OAuthAccounts)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

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
            TwoFactorEnabledAt = entity.TwoFactorEnabledAt
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

        await DbContext.Users.AddAsync(userEntity, cancellationToken);
    }

    public async Task UpdateAsync(User entity, CancellationToken cancellationToken = default)
    {
        // Collect domain events BEFORE updating persistence entity
        CollectDomainEvents(entity);

        // Load the existing user with backup codes for update
        // Note: We don't need to track OAuthAccounts here since they're managed via OAuthAccountRepository
        var existingUser = await DbContext.Users
            .Include(u => u.BackupCodes)
            .FirstOrDefaultAsync(u => u.Id == entity.Id, cancellationToken);

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

        await Task.CompletedTask;
    }

    public async Task DeleteAsync(User entity, CancellationToken cancellationToken = default)
    {
        var userEntity = await DbContext.Users.FindAsync(new object[] { entity.Id }, cancellationToken);
        if (userEntity != null)
        {
            DbContext.Users.Remove(userEntity);
        }
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<List<Api.Infrastructure.Entities.UserBackupCodeEntity>> GetBackupCodesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await DbContext.UserBackupCodes
            .AsNoTracking()
            .Where(bc => bc.UserId == userId)
            .ToListAsync(cancellationToken);
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

        // Reconstruct 2FA state using reflection (properties are private set)
        if (entity.IsTwoFactorEnabled && !string.IsNullOrEmpty(entity.TotpSecretEncrypted))
        {
            var totpSecret = TotpSecret.FromEncrypted(entity.TotpSecretEncrypted);

            // Convert backup codes from persistence to domain
            var backupCodes = entity.BackupCodes
                .Select(bc => BackupCode.FromHashed(bc.CodeHash, bc.IsUsed, bc.UsedAt))
                .ToList();

            // Set properties directly via reflection to avoid mutating TwoFactorEnabledAt
            var totpSecretProp = typeof(User).GetProperty("TotpSecret");
            var isTwoFactorEnabledProp = typeof(User).GetProperty("IsTwoFactorEnabled");
            var twoFactorEnabledAtProp = typeof(User).GetProperty("TwoFactorEnabledAt");
            var backupCodesField = typeof(User).GetField("_backupCodes", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            totpSecretProp?.SetValue(user, totpSecret);
            isTwoFactorEnabledProp?.SetValue(user, entity.IsTwoFactorEnabled);
            twoFactorEnabledAtProp?.SetValue(user, entity.TwoFactorEnabledAt);

            if (backupCodesField != null)
            {
                var backupCodesList = (List<BackupCode>)backupCodesField.GetValue(user)!;
                backupCodesList.Clear();
                backupCodesList.AddRange(backupCodes);
            }
        }

        // Reconstruct OAuth accounts collection using reflection
        if (entity.OAuthAccounts.Any())
        {
            var oauthAccountsField = typeof(User).GetField("_oauthAccounts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (oauthAccountsField != null)
            {
                var oauthAccountsList = (List<OAuthAccount>)oauthAccountsField.GetValue(user)!;
                oauthAccountsList.Clear();

                foreach (var oauthEntity in entity.OAuthAccounts)
                {
                    var oauthAccount = new OAuthAccount(
                        oauthEntity.Id,
                        oauthEntity.UserId,
                        oauthEntity.Provider,
                        oauthEntity.ProviderUserId,
                        oauthEntity.AccessTokenEncrypted,
                        oauthEntity.RefreshTokenEncrypted,
                        oauthEntity.TokenExpiresAt
                    );
                    oauthAccountsList.Add(oauthAccount);
                }
            }
        }

        return user;
    }

    public async Task<int> CountAdminsAsync(CancellationToken cancellationToken = default)
    {
        var adminRole = Role.Admin.Value;
        return await DbContext.Users
            .AsNoTracking()
            .CountAsync(u => u.Role == adminRole, cancellationToken);
    }

    public async Task<List<User>> SearchAsync(string query, int maxResults, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new List<User>();
        }

        // Include OAuthAccounts for domain logic that requires checking auth methods
        var userEntities = await DbContext.Users
            .Include(u => u.BackupCodes)
            .Include(u => u.OAuthAccounts)
            .Where(u => (u.DisplayName != null && u.DisplayName.Contains(query)) || u.Email.Contains(query))
            .OrderBy(u => u.DisplayName ?? u.Email)
            .Take(maxResults)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return userEntities.Select(MapToDomain).ToList();
    }
}
