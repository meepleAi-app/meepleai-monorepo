using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Authentication.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of User repository.
/// Maps between domain User entity and UserEntity persistence model.
/// </summary>
public class UserRepository : IUserRepository
{
    private readonly MeepleAiDbContext _dbContext;

    public UserRepository(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var userEntity = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        return userEntity != null ? MapToDomain(userEntity) : null;
    }

    public async Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default)
    {
        var userEntity = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email.Value, cancellationToken);

        return userEntity != null ? MapToDomain(userEntity) : null;
    }

    public async Task<bool> ExistsByEmailAsync(Email email, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(u => u.Email == email.Value, cancellationToken);
    }

    public async Task<bool> HasAnyUsersAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(cancellationToken);
    }

    public async Task<List<User>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var userEntities = await _dbContext.Users
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return userEntities.Select(MapToDomain).ToList();
    }

    public async Task AddAsync(User entity, CancellationToken cancellationToken = default)
    {
        var userEntity = MapToPersistence(entity);
        await _dbContext.Users.AddAsync(userEntity, cancellationToken);
    }

    public async Task UpdateAsync(User entity, CancellationToken cancellationToken = default)
    {
        var userEntity = MapToPersistence(entity);
        _dbContext.Users.Update(userEntity);
        await Task.CompletedTask;
    }

    public async Task DeleteAsync(User entity, CancellationToken cancellationToken = default)
    {
        var userEntity = await _dbContext.Users.FindAsync(new object[] { entity.Id }, cancellationToken);
        if (userEntity != null)
        {
            _dbContext.Users.Remove(userEntity);
        }
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<List<Api.Infrastructure.Entities.UserBackupCodeEntity>> GetBackupCodesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.UserBackupCodes
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
        var displayName = string.IsNullOrWhiteSpace(entity.DisplayName)
            ? entity.Email
            : entity.DisplayName ?? entity.Email;

        var user = new User(
            id: entity.Id,
            email: email,
            displayName: displayName,
            passwordHash: passwordHash,
            role: role
        );

        // Reconstruct 2FA state using reflection (properties are private set)
        if (entity.IsTwoFactorEnabled && !string.IsNullOrEmpty(entity.TotpSecretEncrypted))
        {
            var totpSecret = TotpSecret.FromEncrypted(entity.TotpSecretEncrypted);

            // Convert backup codes from persistence to domain
            var backupCodes = entity.BackupCodes
                .Select(bc => BackupCode.FromHashed(bc.CodeHash, bc.IsUsed, bc.UsedAt))
                .ToList();

            // Use reflection to call Enable2FA
            var enable2FAMethod = typeof(User).GetMethod("Enable2FA");
            if (enable2FAMethod != null)
            {
                try
                {
                    enable2FAMethod.Invoke(user, new object[] { totpSecret, backupCodes });
                }
                catch (Exception ex)
                {
                    // If Enable2FA throws (e.g., already enabled), set properties directly via reflection
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
            }
        }

        return user;
    }

    public async Task<int> CountAdminsAsync(CancellationToken cancellationToken = default)
    {
        var adminRole = Role.Admin.Value;
        return await _dbContext.Users
            .AsNoTracking()
            .CountAsync(u => u.Role == adminRole, cancellationToken);
    }

    /// <summary>
    /// Maps domain entity to persistence entity.
    /// </summary>
    private static Api.Infrastructure.Entities.UserEntity MapToPersistence(User domainEntity)
    {
        var userEntity = new Api.Infrastructure.Entities.UserEntity
        {
            Id = domainEntity.Id,
            Email = domainEntity.Email.Value,
            DisplayName = domainEntity.DisplayName,
            PasswordHash = domainEntity.PasswordHash.Value,
            Role = domainEntity.Role.Value,
            CreatedAt = domainEntity.CreatedAt,
            TotpSecretEncrypted = domainEntity.TotpSecret?.EncryptedValue,
            IsTwoFactorEnabled = domainEntity.IsTwoFactorEnabled,
            TwoFactorEnabledAt = domainEntity.TwoFactorEnabledAt
        };

        // Map backup codes
        var backupCodeEntities = domainEntity.BackupCodes
            .Select(bc => new Api.Infrastructure.Entities.UserBackupCodeEntity
            {
                Id = Guid.NewGuid(),
                UserId = domainEntity.Id,
                CodeHash = bc.HashedValue,
                IsUsed = bc.IsUsed,
                UsedAt = bc.UsedAt,
                CreatedAt = DateTime.UtcNow
            })
            .ToList();

        userEntity.BackupCodes = backupCodeEntities;

        return userEntity;
    }
}
