using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;
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
        ArgumentNullException.ThrowIfNull(entity);
        // Collect domain events BEFORE mapping to persistence entity
        CollectDomainEvents(entity);

        var userEntity = new Api.Infrastructure.Entities.UserEntity
        {
            Id = entity.Id,
            Email = entity.Email.Value,
            DisplayName = entity.DisplayName,
            PasswordHash = entity.PasswordHash?.Value,
            Role = entity.Role.Value,
            Tier = entity.Tier.Value,
            CreatedAt = entity.CreatedAt,
            TotpSecretEncrypted = entity.TotpSecret?.EncryptedValue,
            IsTwoFactorEnabled = entity.IsTwoFactorEnabled,
            TwoFactorEnabledAt = entity.TwoFactorEnabledAt,
            Language = entity.Language,
            EmailNotifications = entity.EmailNotifications,
            Theme = entity.Theme,
            DataRetentionDays = entity.DataRetentionDays,
            // ISSUE-2886: Suspension fields
            IsSuspended = entity.IsSuspended,
            SuspendedAt = entity.SuspendedAt,
            SuspendReason = entity.SuspendReason,
            // ISSUE-3339: Account Lockout
            FailedLoginAttempts = entity.FailedLoginAttempts,
            LockedUntil = entity.LockedUntil,
            // ISSUE-3672: Email Verification
            EmailVerified = entity.EmailVerified,
            EmailVerifiedAt = entity.EmailVerifiedAt,
            VerificationGracePeriodEndsAt = entity.VerificationGracePeriodEndsAt,
            // Profile & Onboarding
            AvatarUrl = entity.AvatarUrl,
            Bio = entity.Bio,
            OnboardingWizardSeenAt = entity.OnboardingWizardSeenAt,
            OnboardingDismissedAt = entity.OnboardingDismissedAt,
            // Issue #124: Admin invitation flow — pending user fields
            Status = entity.Status.ToString(),
            InvitedByUserId = entity.InvitedByUserId,
            InvitationExpiresAt = entity.InvitationExpiresAt
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
        ArgumentNullException.ThrowIfNull(entity);
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
        existingUser.PasswordHash = entity.PasswordHash?.Value;
        existingUser.Role = entity.Role.Value;
        existingUser.Tier = entity.Tier.Value;
        // Issue #124: Admin invitation flow — update status and invitation fields
        existingUser.Status = entity.Status.ToString();
        existingUser.InvitedByUserId = entity.InvitedByUserId;
        existingUser.InvitationExpiresAt = entity.InvitationExpiresAt;
        existingUser.TotpSecretEncrypted = entity.TotpSecret?.EncryptedValue;
        existingUser.IsTwoFactorEnabled = entity.IsTwoFactorEnabled;
        existingUser.TwoFactorEnabledAt = entity.TwoFactorEnabledAt;

        // Update preferences
        existingUser.Language = entity.Language;
        existingUser.EmailNotifications = entity.EmailNotifications;
        existingUser.Theme = entity.Theme;
        existingUser.DataRetentionDays = entity.DataRetentionDays;

        // ISSUE-2886: Update suspension state
        existingUser.IsSuspended = entity.IsSuspended;
        existingUser.SuspendedAt = entity.SuspendedAt;
        existingUser.SuspendReason = entity.SuspendReason;

        // ISSUE-3141: Update gamification fields
        existingUser.Level = entity.Level;
        existingUser.ExperiencePoints = entity.ExperiencePoints;

        // ISSUE-3339: Update lockout state
        existingUser.FailedLoginAttempts = entity.FailedLoginAttempts;
        existingUser.LockedUntil = entity.LockedUntil;

        // ISSUE-3672: Update email verification state
        existingUser.EmailVerified = entity.EmailVerified;
        existingUser.EmailVerifiedAt = entity.EmailVerifiedAt;
        existingUser.VerificationGracePeriodEndsAt = entity.VerificationGracePeriodEndsAt;

        // Profile & Onboarding
        existingUser.AvatarUrl = entity.AvatarUrl;
        existingUser.Bio = entity.Bio;
        existingUser.OnboardingWizardSeenAt = entity.OnboardingWizardSeenAt;
        existingUser.OnboardingDismissedAt = entity.OnboardingDismissedAt;

        // Issue #323: Update onboarding state
        existingUser.OnboardingCompleted = entity.OnboardingCompleted;
        existingUser.OnboardingSkipped = entity.OnboardingSkipped;
        existingUser.OnboardingCompletedAt = entity.OnboardingCompletedAt;

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
        ArgumentNullException.ThrowIfNull(entity);
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

        if (string.IsNullOrWhiteSpace(entity.Role))
        {
            throw new InvalidOperationException($"Persisted user record {entity.Id} is missing a role.");
        }

        var email = new Email(entity.Email);
        // Issue #124: PasswordHash can be null for pending (invited) users
        var isPendingUser = string.IsNullOrWhiteSpace(entity.PasswordHash);
        var passwordHash = isPendingUser
            ? PasswordHash.Create("TemporaryPlaceholder123!") // Placeholder for constructor; overridden below
            : PasswordHash.FromStored(entity.PasswordHash);
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

        // Issue #124: Restore null PasswordHash for pending users (overrides placeholder)
        if (isPendingUser)
        {
            user.RestorePasswordHash(null);
        }

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
        if (entity.OAuthAccounts.Count > 0)
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

        // ISSUE-2886: Restore suspension state
        if (entity.IsSuspended)
        {
            user.RestoreSuspensionState(entity.IsSuspended, entity.SuspendedAt, entity.SuspendReason);
        }

        // ISSUE-3141: Restore gamification state
        user.RestoreGamificationState(entity.Level, entity.ExperiencePoints);

        // ISSUE-3339: Restore lockout state
        user.RestoreLockoutState(entity.FailedLoginAttempts, entity.LockedUntil);

        // ISSUE-3672: Restore email verification state
        user.RestoreEmailVerificationState(
            entity.EmailVerified,
            entity.EmailVerifiedAt,
            entity.VerificationGracePeriodEndsAt);

        // Profile & Onboarding: Restore state
        user.RestoreOnboardingState(
            entity.AvatarUrl,
            entity.Bio,
            entity.OnboardingWizardSeenAt,
            entity.OnboardingDismissedAt);

        // Issue #323: Restore onboarding state
        user.RestoreOnboardingState(
            entity.OnboardingCompleted,
            entity.OnboardingSkipped,
            entity.OnboardingCompletedAt);

        // Issue #124: Restore account status and invitation state for pending users
        if (!string.IsNullOrWhiteSpace(entity.Status)
            && Enum.TryParse<Api.SharedKernel.Domain.Enums.UserAccountStatus>(entity.Status, out var status))
        {
            user.RestoreAccountStatus(status);
        }

        user.RestoreInvitationState(entity.InvitedByUserId, entity.InvitationExpiresAt);

        return user;
    }

    public async Task<int> CountAdminsAsync(CancellationToken cancellationToken = default)
    {
        var adminRole = Role.Admin.Value;
        var superAdminRole = Role.SuperAdmin.Value;
        return await DbContext.Users
            .AsNoTracking()
            .CountAsync(u => u.Role == adminRole || u.Role == superAdminRole, cancellationToken).ConfigureAwait(false);
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

    public async Task<IReadOnlyList<User>> GetAdminUsersAsync(CancellationToken cancellationToken = default)
    {
        var adminRole = Role.Admin.Value;
        var superAdminRole = Role.SuperAdmin.Value;

        // Include OAuthAccounts for domain logic that requires checking auth methods
        var userEntities = await DbContext.Users
            .Include(u => u.BackupCodes)
            .Include(u => u.OAuthAccounts)
            .Where(u => u.Role == adminRole || u.Role == superAdminRole)
            .AsNoTracking()
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return userEntities.Select(MapToDomain).ToList();
    }
}
