using Microsoft.AspNetCore.DataProtection;

namespace Api.Services;

/// <summary>
/// Provides encryption and decryption services using ASP.NET Core Data Protection API.
/// Thread-safe and supports automatic key rotation.
/// </summary>
public class EncryptionService : IEncryptionService
{
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly ILogger<EncryptionService> _logger;

    public EncryptionService(
        IDataProtectionProvider dataProtectionProvider,
        ILogger<EncryptionService> logger)
    {
        _dataProtectionProvider = dataProtectionProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<string> EncryptAsync(string plaintext, string purpose = "default")
    {
        if (string.IsNullOrEmpty(plaintext))
            throw new ArgumentException("Plaintext cannot be null or empty", nameof(plaintext));

        if (string.IsNullOrWhiteSpace(purpose))
            purpose = "default";

        try
        {
            var protector = _dataProtectionProvider.CreateProtector(purpose);
            var encrypted = protector.Protect(plaintext);
            _logger.LogDebug("Successfully encrypted data with purpose: {Purpose}", purpose);
            return Task.FromResult(encrypted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Encryption failed for purpose: {Purpose}", purpose);
            throw new InvalidOperationException("Encryption operation failed", ex);
        }
    }

    /// <inheritdoc />
    public Task<string> DecryptAsync(string ciphertext, string purpose = "default")
    {
        if (string.IsNullOrEmpty(ciphertext))
            throw new ArgumentException("Ciphertext cannot be null or empty", nameof(ciphertext));

        if (string.IsNullOrWhiteSpace(purpose))
            purpose = "default";

        try
        {
            var protector = _dataProtectionProvider.CreateProtector(purpose);
            var decrypted = protector.Unprotect(ciphertext);
            _logger.LogDebug("Successfully decrypted data with purpose: {Purpose}", purpose);
            return Task.FromResult(decrypted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Decryption failed for purpose: {Purpose}", purpose);
            throw new InvalidOperationException("Decryption operation failed. Key may have been rotated or data corrupted.", ex);
        }
    }
}
