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
    public DateTime CreatedAt { get; private set; }

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
        Role role) : base(id)
    {
        Email = email ?? throw new ArgumentNullException(nameof(email));
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        PasswordHash = passwordHash ?? throw new ArgumentNullException(nameof(passwordHash));
        Role = role ?? throw new ArgumentNullException(nameof(role));
        CreatedAt = DateTime.UtcNow;

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
        // TODO: Add domain event PasswordChanged
    }

    /// <summary>
    /// Updates the user's password (admin-only operation, no current password required).
    /// Use this in admin password reset scenarios.
    /// </summary>
    public void UpdatePassword(PasswordHash newPasswordHash)
    {
        PasswordHash = newPasswordHash;
        // TODO: Add domain event PasswordReset
    }

    /// <summary>
    /// Updates the user's email address.
    /// </summary>
    public void UpdateEmail(Email newEmail)
    {
        if (Email == newEmail)
            return; // No change

        Email = newEmail;
        // TODO: Add domain event EmailChanged
    }

    /// <summary>
    /// Updates the user's display name.
    /// </summary>
    public void UpdateDisplayName(string newDisplayName)
    {
        if (string.IsNullOrWhiteSpace(newDisplayName))
            throw new ValidationException(nameof(DisplayName), "Display name cannot be empty");

        if (DisplayName == newDisplayName)
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

        Role = newRole;
        // TODO: Add domain event RoleChanged
    }

    /// <summary>
    /// Updates the user's role (admin-only operation).
    /// Use this in admin handlers where authorization is already verified.
    /// </summary>
    public void UpdateRole(Role newRole)
    {
        if (Role == newRole)
            return; // No change

        Role = newRole;
        // TODO: Add domain event RoleChanged
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
        if (totpSecret == null)
            throw new ArgumentNullException(nameof(totpSecret));

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

        // TODO: Add domain event TwoFactorEnabled
    }

    /// <summary>
    /// Disables two-factor authentication for this user.
    /// DDD: Clears TotpSecret and all backup codes.
    /// </summary>
    /// <exception cref="DomainException">Thrown when 2FA is not enabled.</exception>
    public void Disable2FA()
    {
        if (!IsTwoFactorEnabled)
            throw new DomainException("Two-factor authentication is not enabled");

        TotpSecret = null;
        IsTwoFactorEnabled = false;
        TwoFactorEnabledAt = null;
        _backupCodes.Clear();

        // TODO: Add domain event TwoFactorDisabled
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
        var code = _backupCodes.FirstOrDefault(bc => bc.HashedValue == backupCodeHash);
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
        return _backupCodes.Any(bc => bc.HashedValue == backupCodeHash && !bc.IsUsed);
    }

    /// <summary>
    /// Links an OAuth provider account to this user.
    /// Business rule: Only one account per provider is allowed.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when account is null.</exception>
    /// <exception cref="DomainException">Thrown when provider is already linked.</exception>
    public void LinkOAuthAccount(OAuthAccount account)
    {
        if (account == null)
            throw new ArgumentNullException(nameof(account));

        // Validate provider is supported (account constructor already validates this, but check again)
        if (!OAuthAccount.SupportedProviders.Contains(account.Provider))
            throw new ValidationException(nameof(account.Provider), $"Unsupported OAuth provider: {account.Provider}");

        // Business rule: One account per provider
        if (_oauthAccounts.Any(a => a.Provider.Equals(account.Provider, StringComparison.OrdinalIgnoreCase)))
            throw new DomainException($"OAuth provider '{account.Provider}' is already linked to this user");

        _oauthAccounts.Add(account);
        // TODO: Add domain event OAuthAccountLinked
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
        // Check BEFORE removing to see what state we'd be in after removal
        if (PasswordHash == null && _oauthAccounts.Count == 1)
            throw new DomainException("Cannot unlink OAuth account: User must have at least one authentication method (password or OAuth)");

        _oauthAccounts.Remove(account);
        // TODO: Add domain event OAuthAccountUnlinked
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
}
