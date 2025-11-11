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

    // 2FA properties
    public string? TotpSecretEncrypted { get; private set; }
    public bool IsTwoFactorEnabled { get; private set; }
    public DateTime? TwoFactorEnabledAt { get; private set; }

    // Navigation properties (not part of domain model, for EF Core only)
    private readonly List<Session> _sessions = new();
    private readonly List<ApiKey> _apiKeys = new();
    public IReadOnlyCollection<Session> Sessions => _sessions.AsReadOnly();
    public IReadOnlyCollection<ApiKey> ApiKeys => _apiKeys.AsReadOnly();

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
    /// Enables two-factor authentication for this user.
    /// </summary>
    public void EnableTwoFactor(string encryptedTotpSecret)
    {
        if (string.IsNullOrWhiteSpace(encryptedTotpSecret))
            throw new ValidationException(nameof(encryptedTotpSecret), "TOTP secret cannot be empty");

        if (IsTwoFactorEnabled)
            throw new DomainException("Two-factor authentication is already enabled");

        TotpSecretEncrypted = encryptedTotpSecret;
        IsTwoFactorEnabled = true;
        TwoFactorEnabledAt = DateTime.UtcNow;

        // TODO: Add domain event TwoFactorEnabled
    }

    /// <summary>
    /// Disables two-factor authentication for this user.
    /// </summary>
    public void DisableTwoFactor()
    {
        if (!IsTwoFactorEnabled)
            throw new DomainException("Two-factor authentication is not enabled");

        TotpSecretEncrypted = null;
        IsTwoFactorEnabled = false;
        TwoFactorEnabledAt = null;

        // TODO: Add domain event TwoFactorDisabled
    }

    /// <summary>
    /// Checks if this user requires two-factor authentication.
    /// </summary>
    public bool RequiresTwoFactor() => IsTwoFactorEnabled;
}
