using Api.BoundedContexts.Authentication.Domain.Events;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Administration.Domain.Enums; // Epic #4068: UserAccountStatus
using Api.SharedKernel.Domain.Entities;
using Api.SharedKernel.Domain.Exceptions;

namespace Api.BoundedContexts.Authentication.Domain.Entities;

/// <summary>
/// User aggregate root.
/// Represents an authenticated user in the system with identity, credentials, and role.
/// </summary>
public sealed class User : AggregateRoot<Guid>
{
    public Email Email { get; private set; }
    public string DisplayName { get; private set; }
    public PasswordHash PasswordHash { get; private set; }
    public Role Role { get; private set; }
    public UserTier Tier { get; private set; }
    public UserAccountStatus Status { get; private set; } // Epic #4068 (replaces IsSuspended)
    public DateTime CreatedAt { get; private set; }
    public bool IsDemoAccount { get; private set; }

    // Suspension properties
    public bool IsSuspended { get; private set; }
    public DateTime? SuspendedAt { get; private set; }
    public string? SuspendReason { get; private set; }

    // User preferences
    public string Language { get; private set; }
    public bool EmailNotifications { get; private set; }
    public string Theme { get; private set; }
    public int DataRetentionDays { get; private set; }

    // Gamification properties (Issue #3141)
    public int Level { get; private set; }
    public int ExperiencePoints { get; private set; }

    // Account lockout properties (Issue #3339)
    public int FailedLoginAttempts { get; private set; }
    public DateTime? LockedUntil { get; private set; }

    // Email verification properties (Issue #3672)
    public bool EmailVerified { get; private set; }
    public DateTime? EmailVerifiedAt { get; private set; }
    public DateTime? VerificationGracePeriodEndsAt { get; private set; }

    // 2FA properties (DDD Value Objects)
    public TotpSecret? TotpSecret { get; private set; }
    public bool IsTwoFactorEnabled { get; private set; }
    public DateTime? TwoFactorEnabledAt { get; private set; }

    // Backup codes collection (DDD)
    private readonly List<BackupCode> _backupCodes = new();
    public IReadOnlyCollection<BackupCode> BackupCodes => _backupCodes.AsReadOnly();

    // Onboarding preferences (Issue #124: Invitation system)
    public List<string>? Interests { get; private set; }

    // Issue #323: Onboarding completion tracking
    public bool OnboardingCompleted { get; private set; }
    public bool OnboardingSkipped { get; private set; }
    public DateTime? OnboardingCompletedAt { get; private set; }

    // Navigation properties (not part of domain model, for EF Core only)
    private readonly List<Session> _sessions = new();
    private readonly List<ApiKey> _apiKeys = new();
    private readonly List<OAuthAccount> _oauthAccounts = new();
    public IReadOnlyCollection<Session> Sessions => _sessions.AsReadOnly();
    public IReadOnlyCollection<ApiKey> ApiKeys => _apiKeys.AsReadOnly();
    public IReadOnlyCollection<OAuthAccount> OAuthAccounts => _oauthAccounts.AsReadOnly();

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
#pragma warning disable CS8618 // Non-nullable property must contain a non-null value when exiting constructor
    private User() : base()
#pragma warning restore CS8618
    {
    }

    /// <summary>
    /// Creates a new user.
    /// </summary>
    public User(
        Guid id,
        Email email,
        string displayName,
        PasswordHash passwordHash,
        Role role,
        UserTier? tier = null) : base(id)
    {
        Email = email ?? throw new ArgumentNullException(nameof(email));
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        PasswordHash = passwordHash ?? throw new ArgumentNullException(nameof(passwordHash));
        Role = role ?? throw new ArgumentNullException(nameof(role));
        Tier = tier ?? UserTier.Free; // Default to Free tier
        Status = UserAccountStatus.Active; // Epic #4068
        CreatedAt = DateTime.UtcNow;

        // Default preferences
        Language = "en";
        EmailNotifications = true;
        Theme = "system";
        DataRetentionDays = 90;

        // Default gamification (Issue #3141)
        Level = 1;
        ExperiencePoints = 0;

        // Default lockout state (Issue #3339)
        FailedLoginAttempts = 0;
        LockedUntil = null;

        IsTwoFactorEnabled = false;
    }

    /// <summary>
    /// Verifies if the provided password is correct.
    /// </summary>
    public bool VerifyPassword(string plaintextPassword)
    {
        return PasswordHash.Verify(plaintextPassword);
    }

    /// <summary>
    /// Changes the user's password (requires current password verification).
    /// </summary>
    public void ChangePassword(string currentPassword, PasswordHash newPasswordHash)
    {
        if (!VerifyPassword(currentPassword))
            throw new DomainException("Current password is incorrect");

        PasswordHash = newPasswordHash;
        AddDomainEvent(new PasswordChangedEvent(Id));
    }

    /// <summary>
    /// Updates the user's password (admin-only operation, no current password required).
    /// Use this in admin password reset scenarios.
    /// </summary>
    public void UpdatePassword(PasswordHash newPasswordHash)
    {
        PasswordHash = newPasswordHash;
        AddDomainEvent(new PasswordResetEvent(Id));
    }

    /// <summary>
    /// Marks this user as a demo account.
    /// Demo accounts can authenticate without password validation for demonstration purposes.
    /// </summary>
    public void MarkAsDemoAccount()
    {
        IsDemoAccount = true;
    }

    /// <summary>
    /// Suspends the user account. Suspended users cannot login.
    /// Epic #4068: Updated to use UserAccountStatus
    /// </summary>
    public void Suspend(string? reason = null)
    {
        if (Status == UserAccountStatus.Suspended)
            throw new DomainException("User is already suspended");
        if (Status == UserAccountStatus.Banned)
            throw new DomainException("Cannot suspend banned user");

        Status = UserAccountStatus.Suspended;
        IsSuspended = true;
        SuspendedAt = DateTime.UtcNow;
        SuspendReason = reason;
        AddDomainEvent(new UserSuspendedEvent(Id, reason));
    }

    /// <summary>
    /// Bans the user account permanently (Epic #4068)
    /// </summary>
    public void Ban(string reason)
    {
        if (Status == UserAccountStatus.Banned)
            throw new DomainException("User is already banned");

        Status = UserAccountStatus.Banned;
        IsSuspended = true; // Maintain backward compat
        SuspendedAt = DateTime.UtcNow;
        SuspendReason = reason;
        AddDomainEvent(new UserSuspendedEvent(Id, reason)); // Reuse event
    }

    /// <summary>
    /// Unsuspends (reactivates) the user account.
    /// Epic #4068: Updated to use UserAccountStatus
    /// </summary>
    public void Unsuspend()
    {
        if (!IsSuspended)
            throw new DomainException("User is not suspended");
        if (Status == UserAccountStatus.Banned)
            throw new DomainException("Cannot unsuspend banned user - use Unban instead");

        Status = UserAccountStatus.Active;
        IsSuspended = false;
        SuspendedAt = null;
        SuspendReason = null;
        AddDomainEvent(new UserUnsuspendedEvent(Id));
    }

    /// <summary>
    /// Unbans the user account (Epic #4068)
    /// </summary>
    public void Unban()
    {
        if (Status != UserAccountStatus.Banned)
            throw new DomainException("User is not banned");

        Status = UserAccountStatus.Active;
        IsSuspended = false;
        SuspendedAt = null;
        SuspendReason = null;
        AddDomainEvent(new UserUnsuspendedEvent(Id));
    }

    /// <summary>
    /// Checks if the user can authenticate (not suspended/banned).
    /// Epic #4068: Updated to check UserAccountStatus
    /// </summary>
    public bool CanAuthenticate() => Status == UserAccountStatus.Active;

    /// <summary>
    /// Updates the user's email address.
    /// </summary>
    public void UpdateEmail(Email newEmail)
    {
        ArgumentNullException.ThrowIfNull(newEmail);
        if (Email == newEmail)
            return; // No change

        var oldEmail = Email;
        Email = newEmail;
        AddDomainEvent(new EmailChangedEvent(Id, oldEmail, newEmail));
    }

    /// <summary>
    /// Updates the user's display name.
    /// </summary>
    public void UpdateDisplayName(string newDisplayName)
    {
        if (string.IsNullOrWhiteSpace(newDisplayName))
            throw new ValidationException(nameof(DisplayName), "Display name cannot be empty");

        if (string.Equals(DisplayName, newDisplayName, StringComparison.Ordinal))
            return; // No change

        DisplayName = newDisplayName;
    }

    /// <summary>
    /// Assigns a new role to the user.
    /// </summary>
    public void AssignRole(Role newRole, Role requesterRole)
    {
        ArgumentNullException.ThrowIfNull(newRole);
        ArgumentNullException.ThrowIfNull(requesterRole);
        // Only admins can assign roles
        if (!requesterRole.IsAdmin())
            throw new DomainException("Only administrators can assign roles");

        // Cannot assign admin role to self (must be done by another admin)
        if (newRole.IsAdmin() && Role.IsAdmin())
            throw new DomainException("Cannot modify admin role through self-service");

        var oldRole = Role;
        Role = newRole;
        AddDomainEvent(new RoleChangedEvent(Id, oldRole, newRole));
    }

    /// <summary>
    /// Updates the user's role (admin-only operation).
    /// Use this in admin handlers where authorization is already verified.
    /// </summary>
    public void UpdateRole(Role newRole)
    {
        ArgumentNullException.ThrowIfNull(newRole);
        if (Role == newRole)
            return; // No change

        var oldRole = Role;
        Role = newRole;
        AddDomainEvent(new RoleChangedEvent(Id, oldRole, newRole));
    }

    /// <summary>
    /// Sets the user's level (admin-only operation).
    /// Issue #3141: Allow admins to manually adjust user level.
    /// </summary>
    /// <param name="level">New level value (must be 0-100)</param>
    /// <exception cref="ArgumentException">Thrown when level is out of range</exception>
    public void SetLevel(int level)
    {
        if (level < 0)
            throw new ArgumentException("Level cannot be negative", nameof(level));
        if (level > 100)
            throw new ArgumentException("Level cannot exceed 100", nameof(level));

        if (Level == level)
            return; // No change

        var oldLevel = Level;
        Level = level;
        AddDomainEvent(new UserLevelChangedEvent(Id, oldLevel, level));
    }

    /// <summary>
    /// Adds experience points to the user (future gamification).
    /// Issue #3141: Foundation for XP-based level progression.
    /// </summary>
    /// <param name="points">Experience points to add (must be >= 0)</param>
    /// <exception cref="ArgumentException">Thrown when points is negative</exception>
    public void AddExperience(int points)
    {
        if (points < 0)
            throw new ArgumentException("Experience points cannot be negative", nameof(points));

        ExperiencePoints += points;
    }

    #region Account Lockout (Issue #3339)

    /// <summary>
    /// Default maximum failed login attempts before account lockout.
    /// </summary>
    public const int DefaultMaxFailedAttempts = 5;

    /// <summary>
    /// Default lockout duration in minutes.
    /// </summary>
    public const int DefaultLockoutDurationMinutes = 15;

    /// <summary>
    /// Checks if the account is currently locked out.
    /// </summary>
    /// <param name="timeProvider">Optional time provider for testing.</param>
    /// <returns>True if account is locked and lockout period has not expired.</returns>
    public bool IsLockedOut(TimeProvider? timeProvider = null)
    {
        if (LockedUntil == null) return false;
        var now = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        return now < LockedUntil;
    }

    /// <summary>
    /// Gets the remaining lockout duration.
    /// </summary>
    /// <param name="timeProvider">Optional time provider for testing.</param>
    /// <returns>Remaining lockout time, or TimeSpan.Zero if not locked.</returns>
    public TimeSpan GetRemainingLockoutDuration(TimeProvider? timeProvider = null)
    {
        if (LockedUntil == null) return TimeSpan.Zero;
        var now = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        var remaining = LockedUntil.Value - now;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    /// <summary>
    /// Records a failed login attempt and locks account if threshold exceeded.
    /// Issue #3339: Account lockout after failed login attempts.
    /// </summary>
    /// <param name="ipAddress">IP address of the failed attempt (for audit).</param>
    /// <param name="maxAttempts">Maximum failed attempts before lockout (default: 5).</param>
    /// <param name="lockoutMinutes">Lockout duration in minutes (default: 15).</param>
    /// <param name="timeProvider">Optional time provider for testing.</param>
    /// <returns>True if account was locked as a result of this attempt.</returns>
    public bool RecordFailedLogin(
        string? ipAddress = null,
        int maxAttempts = DefaultMaxFailedAttempts,
        int lockoutMinutes = DefaultLockoutDurationMinutes,
        TimeProvider? timeProvider = null)
    {
        // If already locked, don't increment counter
        if (IsLockedOut(timeProvider))
            return false;

        FailedLoginAttempts++;

        if (FailedLoginAttempts >= maxAttempts)
        {
            var now = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
            LockedUntil = now.AddMinutes(lockoutMinutes);
            AddDomainEvent(new AccountLockedEvent(Id, FailedLoginAttempts, LockedUntil.Value, ipAddress));
            return true;
        }

        return false;
    }

    /// <summary>
    /// Records a successful login and resets the failed attempts counter.
    /// Issue #3339: Reset counter on successful login.
    /// </summary>
    public void RecordSuccessfulLogin()
    {
        if (FailedLoginAttempts > 0 || LockedUntil != null)
        {
            FailedLoginAttempts = 0;
            LockedUntil = null;
        }
    }

    /// <summary>
    /// Manually unlocks the account (admin operation).
    /// Issue #3339: Admin can manually unlock accounts.
    /// </summary>
    /// <param name="adminId">ID of the admin performing the unlock.</param>
    /// <exception cref="DomainException">Thrown when account is not locked.</exception>
    public void Unlock(Guid adminId)
    {
        if (LockedUntil == null && FailedLoginAttempts == 0)
            throw new DomainException("Account is not locked");

        FailedLoginAttempts = 0;
        LockedUntil = null;
        AddDomainEvent(new AccountUnlockedEvent(Id, wasManualUnlock: true, adminId));
    }

    #endregion

    /// <summary>
    /// Enables two-factor authentication for this user with TOTP secret and backup codes.
    /// DDD: Uses TotpSecret and BackupCode value objects.
    /// </summary>
    /// <param name="totpSecret">Encrypted TOTP secret value object.</param>
    /// <param name="backupCodes">List of backup code value objects (optional for testing).</param>
    /// <exception cref="ArgumentNullException">Thrown when totpSecret is null.</exception>
    /// <exception cref="DomainException">Thrown when 2FA is already enabled or backup codes are invalid.</exception>
    public void Enable2FA(TotpSecret totpSecret, List<BackupCode>? backupCodes = null)
    {
        ArgumentNullException.ThrowIfNull(totpSecret);


        if (IsTwoFactorEnabled)
            throw new DomainException("Two-factor authentication is already enabled");

        // Validate backup codes if provided
        if (backupCodes != null && backupCodes.Any(bc => bc.IsUsed))
            throw new DomainException("Cannot enable 2FA with used backup codes");

        TotpSecret = totpSecret;
        IsTwoFactorEnabled = true;
        TwoFactorEnabledAt = DateTime.UtcNow;

        // Replace backup codes if provided
        _backupCodes.Clear();
        if (backupCodes != null)
        {
            _backupCodes.AddRange(backupCodes);
        }

        AddDomainEvent(new TwoFactorEnabledEvent(Id, _backupCodes.Count));
    }

    /// <summary>
    /// Disables two-factor authentication for this user.
    /// DDD: Clears TotpSecret and all backup codes.
    /// </summary>
    /// <param name="wasAdminOverride">Whether this was an admin override for account recovery.</param>
    /// <exception cref="DomainException">Thrown when 2FA is not enabled.</exception>
    public void Disable2FA(bool wasAdminOverride = false)
    {
        if (!IsTwoFactorEnabled)
            throw new DomainException("Two-factor authentication is not enabled");

        TotpSecret = null;
        IsTwoFactorEnabled = false;
        TwoFactorEnabledAt = null;
        _backupCodes.Clear();

        AddDomainEvent(new TwoFactorDisabledEvent(Id, wasAdminOverride));
    }

    /// <summary>
    /// Checks if this user requires two-factor authentication.
    /// </summary>
    public bool RequiresTwoFactor() => IsTwoFactorEnabled;

    /// <summary>
    /// Marks a backup code as used.
    /// DDD: Enforces single-use business rule through BackupCode value object.
    /// </summary>
    /// <param name="backupCodeHash">The hash of the backup code to mark as used.</param>
    /// <param name="usedAt">The timestamp when the code was used.</param>
    /// <exception cref="DomainException">Thrown when code not found or already used.</exception>
    public void UseBackupCode(string backupCodeHash, DateTime usedAt)
    {
        var code = _backupCodes.FirstOrDefault(bc => string.Equals(bc.HashedValue, backupCodeHash, StringComparison.Ordinal));
        if (code == null)
            throw new DomainException("Backup code not found");

        code.MarkAsUsed(usedAt);
    }

    /// <summary>
    /// Gets the count of unused backup codes.
    /// </summary>
    public int GetUnusedBackupCodesCount()
    {
        return _backupCodes.Count(bc => !bc.IsUsed);
    }

    /// <summary>
    /// Checks if a backup code exists and is unused.
    /// </summary>
    public bool HasUnusedBackupCode(string backupCodeHash)
    {
        return _backupCodes.Any(bc => string.Equals(bc.HashedValue, backupCodeHash, StringComparison.Ordinal) && !bc.IsUsed);
    }

    /// <summary>
    /// Links an OAuth provider account to this user.
    /// Business rule: Only one account per provider is allowed.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when account is null.</exception>
    /// <exception cref="DomainException">Thrown when provider is already linked.</exception>
    public void LinkOAuthAccount(OAuthAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);


        // Validate provider is supported (account constructor already validates this, but check again)
        if (!OAuthAccount.SupportedProviders.Contains(account.Provider))
            throw new ValidationException(nameof(account.Provider), $"Unsupported OAuth provider: {account.Provider}");

        // Business rule: One account per provider
        if (_oauthAccounts.Any(a => a.Provider.Equals(account.Provider, StringComparison.OrdinalIgnoreCase)))
            throw new DomainException($"OAuth provider '{account.Provider}' is already linked to this user");

        _oauthAccounts.Add(account);
        AddDomainEvent(new OAuthAccountLinkedEvent(Id, account.Provider, account.ProviderUserId));
    }

    /// <summary>
    /// Unlinks an OAuth provider account from this user.
    /// Business rule: Cannot unlink if it's the only authentication method (prevents lockout).
    /// </summary>
    /// <exception cref="ValidationException">Thrown when provider is null or empty.</exception>
    /// <exception cref="DomainException">Thrown when provider is not linked or unlinking would cause lockout.</exception>
    public void UnlinkOAuthAccount(string provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
            throw new ValidationException(nameof(provider), "Provider cannot be empty");

        // Find the account
        var account = _oauthAccounts.FirstOrDefault(a => a.Provider.Equals(provider, StringComparison.OrdinalIgnoreCase));
        if (account == null)
            throw new DomainException($"OAuth provider '{provider}' is not linked to this user");

        // Business rule: Cannot unlink if it would leave user with no auth methods (prevent lockout)
        // User must have EITHER a password OR at least 2 OAuth accounts (so after removing one, at least 1 remains)
        bool hasPassword = PasswordHash != null;
        bool willHaveOAuthAfterUnlink = _oauthAccounts.Count > 1;

        if (!hasPassword && !willHaveOAuthAfterUnlink)
            throw new DomainException("Cannot unlink OAuth account: User must have at least one authentication method (password or OAuth) to prevent account lockout");

        _oauthAccounts.Remove(account);
        AddDomainEvent(new OAuthAccountUnlinkedEvent(Id, provider));
    }

    /// <summary>
    /// Retrieves an OAuth account for the specified provider.
    /// </summary>
    /// <param name="provider">The OAuth provider (e.g., "google", "discord", "github").</param>
    /// <returns>The OAuth account if found; otherwise, null.</returns>
    public OAuthAccount? GetOAuthAccount(string provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
            return null;

        return _oauthAccounts.FirstOrDefault(a => a.Provider.Equals(provider, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Checks if the user has an OAuth account linked for the specified provider.
    /// </summary>
    /// <param name="provider">The OAuth provider (e.g., "google", "discord", "github").</param>
    /// <returns>True if the provider is linked; otherwise, false.</returns>
    public bool HasOAuthAccount(string provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
            return false;

        return _oauthAccounts.Any(a => a.Provider.Equals(provider, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Checks if the user has at least one authentication method available.
    /// Used to prevent lockout when unlinking OAuth accounts.
    /// </summary>
    /// <returns>True if user has password OR at least one OAuth account; otherwise, false.</returns>
    public bool HasAnyAuthenticationMethod()
    {
        // User has password authentication
        bool hasPassword = PasswordHash != null;

        // User has at least one OAuth account
        bool hasOAuth = _oauthAccounts.Count > 0;

        return hasPassword || hasOAuth;
    }

    /// <summary>
    /// Updates the user's subscription tier.
    /// Only admins can change user tiers.
    /// </summary>
    /// <param name="newTier">The new tier to assign.</param>
    /// <param name="requesterRole">The role of the user requesting the tier change.</param>
    /// <exception cref="ArgumentNullException">Thrown when newTier or requesterRole is null.</exception>
    /// <exception cref="DomainException">Thrown when requester is not an admin.</exception>
    public void UpdateTier(UserTier newTier, Role requesterRole)
    {
        ArgumentNullException.ThrowIfNull(newTier);


        ArgumentNullException.ThrowIfNull(requesterRole);


        // Only admins can change user tiers
        if (!requesterRole.IsAdmin())
            throw new DomainException("Only administrators can change user tiers");

        if (Tier == newTier)
            return; // No change

        var oldTier = Tier;
        Tier = newTier;
        AddDomainEvent(new UserTierChangedEvent(Id, oldTier, newTier));
    }

    /// <summary>
    /// Updates the user's preferences (language, theme, notifications, data retention).
    /// </summary>
    public void UpdatePreferences(string language, string theme, bool emailNotifications, int dataRetentionDays)
    {
        if (string.IsNullOrWhiteSpace(language))
            throw new ValidationException(nameof(language), "Language cannot be empty");

        if (string.IsNullOrWhiteSpace(theme))
            throw new ValidationException(nameof(theme), "Theme cannot be empty");

        var validThemes = new[] { "light", "dark", "system" };
        if (!validThemes.Contains(theme, StringComparer.Ordinal))
            throw new ValidationException(nameof(theme), $"Theme must be one of: {string.Join(", ", validThemes)}");

        if (dataRetentionDays <= 0)
            throw new ValidationException(nameof(dataRetentionDays), "Data retention days must be positive");

        Language = language;
        Theme = theme;
        EmailNotifications = emailNotifications;
        DataRetentionDays = dataRetentionDays;
    }

    /// <summary>
    /// Updates the user's onboarding interest selections.
    /// Issue #124: Invitation system onboarding wizard.
    /// </summary>
    /// <param name="interests">List of interest tags selected by the user</param>
    public void UpdateInterests(List<string>? interests)
    {
        Interests = interests;
    }

    /// <summary>
    /// Marks the user's onboarding as completed.
    /// Issue #323: Onboarding completion tracking.
    /// </summary>
    public void CompleteOnboarding()
    {
        OnboardingCompleted = true;
        OnboardingSkipped = false;
        OnboardingCompletedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the user's onboarding as skipped.
    /// Issue #323: Onboarding completion tracking.
    /// </summary>
    public void SkipOnboarding()
    {
        OnboardingCompleted = true;
        OnboardingSkipped = true;
        OnboardingCompletedAt = DateTime.UtcNow;
    }

    #region Persistence Hydration Methods (internal - S3011 fix)

    /// <summary>
    /// Restores 2FA state from persistence layer.
    /// Internal method to avoid reflection in repository (S3011 compliance).
    /// Should only be called by UserRepository during entity materialization.
    /// </summary>
    internal void Restore2FAState(
        TotpSecret? totpSecret,
        bool isTwoFactorEnabled,
        DateTime? twoFactorEnabledAt,
        IEnumerable<BackupCode> backupCodes)
    {
        TotpSecret = totpSecret;
        IsTwoFactorEnabled = isTwoFactorEnabled;
        TwoFactorEnabledAt = twoFactorEnabledAt;
        _backupCodes.Clear();
        _backupCodes.AddRange(backupCodes);
    }

    /// <summary>
    /// Restores OAuth accounts from persistence layer.
    /// Internal method to avoid reflection in repository (S3011 compliance).
    /// Should only be called by UserRepository during entity materialization.
    /// </summary>
    internal void RestoreOAuthAccounts(IEnumerable<OAuthAccount> oauthAccounts)
    {
        _oauthAccounts.Clear();
        _oauthAccounts.AddRange(oauthAccounts);
    }

    /// <summary>
    /// Restores suspension state from persistence layer.
    /// Internal method to avoid reflection in repository (S3011 compliance).
    /// Should only be called by UserRepository during entity materialization.
    /// </summary>
    internal void RestoreSuspensionState(bool isSuspended, DateTime? suspendedAt, string? suspendReason)
    {
        IsSuspended = isSuspended;
        SuspendedAt = suspendedAt;
        SuspendReason = suspendReason;
    }

    /// <summary>
    /// Restores gamification state (Level/XP) from persistence layer.
    /// Issue #3141: Internal method to avoid reflection in repository (S3011 compliance).
    /// Should only be called by UserRepository during entity materialization.
    /// </summary>
    internal void RestoreGamificationState(int level, int experiencePoints)
    {
        Level = level;
        ExperiencePoints = experiencePoints;
    }

    /// <summary>
    /// Restores lockout state from persistence layer.
    /// Issue #3339: Internal method to avoid reflection in repository (S3011 compliance).
    /// Should only be called by UserRepository during entity materialization.
    /// </summary>
    internal void RestoreLockoutState(int failedLoginAttempts, DateTime? lockedUntil)
    {
        FailedLoginAttempts = failedLoginAttempts;
        LockedUntil = lockedUntil;
    }

    /// <summary>
    /// Verifies the user's email address.
    /// Issue #3672: Email verification flow.
    /// </summary>
    public void VerifyEmail()
    {
        if (EmailVerified)
            return; // Already verified, idempotent

        EmailVerified = true;
        EmailVerifiedAt = DateTime.UtcNow;
        VerificationGracePeriodEndsAt = null; // Clear grace period

        AddDomainEvent(new EmailVerifiedEvent(Id, EmailVerifiedAt.Value));
    }

    /// <summary>
    /// Checks if user is currently in the email verification grace period.
    /// Issue #3672: 7-day grace period for existing users.
    /// </summary>
    /// <param name="timeProvider">Optional time provider for testability (matches IsLockedOut pattern)</param>
    public bool IsInGracePeriod(TimeProvider? timeProvider = null)
    {
        if (VerificationGracePeriodEndsAt == null) return false;
        var now = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        return now < VerificationGracePeriodEndsAt;
    }

    /// <summary>
    /// Checks if user requires email verification (not verified and past grace period).
    /// Issue #3672: Used by EmailVerificationMiddleware for enforcement.
    /// </summary>
    /// <param name="timeProvider">Optional time provider for testability</param>
    public bool RequiresVerification(TimeProvider? timeProvider = null) =>
        !EmailVerified && !IsInGracePeriod(timeProvider);

    /// <summary>
    /// Sets the email verification grace period.
    /// Issue #3672: Called during migration for existing users.
    /// </summary>
    /// <param name="gracePeriodEndsAt">When the grace period ends (UTC)</param>
    public void SetVerificationGracePeriod(DateTime gracePeriodEndsAt)
    {
        if (EmailVerified)
            return; // Already verified, no grace period needed

        VerificationGracePeriodEndsAt = gracePeriodEndsAt;
    }

    /// <summary>
    /// Restores email verification state from persistence layer.
    /// Issue #3672: Internal method to avoid reflection in repository (S3011 compliance).
    /// Should only be called by UserRepository during entity materialization.
    /// </summary>
    internal void RestoreEmailVerificationState(bool emailVerified, DateTime? emailVerifiedAt, DateTime? verificationGracePeriodEndsAt)
    {
        EmailVerified = emailVerified;
        EmailVerifiedAt = emailVerifiedAt;
        VerificationGracePeriodEndsAt = verificationGracePeriodEndsAt;
    }

    /// <summary>
    /// Restores onboarding state from persistence layer.
    /// Issue #323: Onboarding completion tracking.
    /// </summary>
    internal void RestoreOnboardingState(bool onboardingCompleted, bool onboardingSkipped, DateTime? onboardingCompletedAt)
    {
        OnboardingCompleted = onboardingCompleted;
        OnboardingSkipped = onboardingSkipped;
        OnboardingCompletedAt = onboardingCompletedAt;
    }

    #endregion
}
