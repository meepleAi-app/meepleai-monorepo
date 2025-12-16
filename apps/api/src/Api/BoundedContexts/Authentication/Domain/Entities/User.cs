using Api.BoundedContexts.Authentication.Domain.Events;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
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
    public DateTime CreatedAt { get; private set; }
    public bool IsDemoAccount { get; private set; }

    // User preferences
    public string Language { get; private set; }
    public bool EmailNotifications { get; private set; }
    public string Theme { get; private set; }
    public int DataRetentionDays { get; private set; }

    // 2FA properties (DDD Value Objects)
    public TotpSecret? TotpSecret { get; private set; }
    public bool IsTwoFactorEnabled { get; private set; }
    public DateTime? TwoFactorEnabledAt { get; private set; }

    // Backup codes collection (DDD)
    private readonly List<BackupCode> _backupCodes = new();
    public IReadOnlyCollection<BackupCode> BackupCodes => _backupCodes.AsReadOnly();

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
        CreatedAt = DateTime.UtcNow;

        // Default preferences
        Language = "en";
        EmailNotifications = true;
        Theme = "system";
        DataRetentionDays = 90;

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
    /// Updates the user's email address.
    /// </summary>
    public void UpdateEmail(Email newEmail)
    {
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
        if (Role == newRole)
            return; // No change

        var oldRole = Role;
        Role = newRole;
        AddDomainEvent(new RoleChangedEvent(Id, oldRole, newRole));
    }

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
        bool hasOAuth = _oauthAccounts.Any();

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

    #endregion
}
