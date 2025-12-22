using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.Authentication.Domain.ValueObjects;

/// <summary>
/// Value object representing a hashed backup code for two-factor authentication.
/// Backup codes are single-use recovery codes in case TOTP is unavailable.
/// Stores the PBKDF2 hash of the code, not the plaintext.
/// </summary>
public sealed class BackupCode : ValueObject
{
    public string HashedValue { get; }
    public bool IsUsed { get; private set; }
    public DateTime? UsedAt { get; private set; }

    /// <summary>
    /// Private constructor for domain reconstruction.
    /// </summary>
    private BackupCode(string hashedValue, bool isUsed = false, DateTime? usedAt = null)
    {
        if (string.IsNullOrWhiteSpace(hashedValue))
            throw new ValidationException(nameof(BackupCode), "Backup code hash cannot be empty");

        if (isUsed && usedAt == null)
            throw new ValidationException(nameof(BackupCode), "Used backup code must have UsedAt timestamp");

        HashedValue = hashedValue;
        IsUsed = isUsed;
        UsedAt = usedAt;
    }

    /// <summary>
    /// Creates a backup code from a hashed value.
    /// Used when storing/retrieving from database.
    /// </summary>
    /// <param name="hashedValue">The PBKDF2 hash of the backup code.</param>
    /// <param name="isUsed">Whether this code has been used.</param>
    /// <param name="usedAt">When the code was used (if applicable).</param>
    /// <returns>A new BackupCode instance.</returns>
    /// <exception cref="ValidationException">Thrown when hashedValue is null, empty, or whitespace.</exception>
    public static BackupCode FromHashed(string hashedValue, bool isUsed = false, DateTime? usedAt = null)
    {
        // Validate BEFORE calling constructor
        if (hashedValue == null)
            throw new ValidationException(nameof(BackupCode), "Backup code hash cannot be null");

        if (string.IsNullOrWhiteSpace(hashedValue))
            throw new ValidationException(nameof(BackupCode), "Backup code hash cannot be empty");

        return new BackupCode(hashedValue, isUsed, usedAt);
    }

    /// <summary>
    /// Marks this backup code as used.
    /// Enforces single-use business rule.
    /// </summary>
    /// <param name="usedAt">The timestamp when the code was used.</param>
    /// <exception cref="DomainException">Thrown if the code is already used.</exception>
    public void MarkAsUsed(DateTime usedAt)
    {
        if (IsUsed)
            throw new DomainException("Backup code has already been used");

        IsUsed = true;
        UsedAt = usedAt;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return HashedValue;
        yield return IsUsed;
        yield return UsedAt;
    }

    public override string ToString() => IsUsed
        ? $"[BACKUP_CODE_USED:{UsedAt:yyyy-MM-dd HH:mm:ss}]"
        : "[BACKUP_CODE_UNUSED]";

    public static implicit operator string(BackupCode code)
    {
        return code.HashedValue;
    }
}
