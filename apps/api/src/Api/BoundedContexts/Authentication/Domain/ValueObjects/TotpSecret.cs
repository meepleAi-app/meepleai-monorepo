using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.Authentication.Domain.ValueObjects;

/// <summary>
/// Value object representing an encrypted TOTP secret.
/// Encapsulates the encrypted secret string for two-factor authentication.
/// The actual encryption/decryption is handled by infrastructure services.
/// </summary>
public sealed class TotpSecret : ValueObject
{
    public string EncryptedValue { get; }

    /// <summary>
    /// Private constructor for domain reconstruction.
    /// </summary>
    private TotpSecret(string encryptedValue)
    {
        if (string.IsNullOrWhiteSpace(encryptedValue))
            throw new ValidationException(nameof(TotpSecret), "TOTP secret cannot be empty");

        EncryptedValue = encryptedValue;
    }

    /// <summary>
    /// Creates a TOTP secret from an encrypted value.
    /// Used when storing/retrieving from database.
    /// </summary>
    /// <param name="encryptedValue">The encrypted TOTP secret (base64 encoded).</param>
    /// <returns>A new TotpSecret instance.</returns>
    public static TotpSecret FromEncrypted(string encryptedValue)
    {
        if (string.IsNullOrWhiteSpace(encryptedValue))
            throw new ValidationException(nameof(TotpSecret), "Encrypted TOTP secret cannot be empty");

        return new TotpSecret(encryptedValue);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return EncryptedValue;
    }

    public override string ToString() => "[TOTP_SECRET_REDACTED]";

    public static implicit operator string(TotpSecret secret)
    {
        return secret.EncryptedValue;
    }
}
