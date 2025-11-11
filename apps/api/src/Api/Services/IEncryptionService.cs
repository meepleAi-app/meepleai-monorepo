namespace Api.Services;

/// <summary>
/// Provides encryption and decryption services for sensitive data (OAuth tokens, etc.)
/// Uses ASP.NET Core Data Protection API for secure key management and rotation.
/// </summary>
public interface IEncryptionService
{
    /// <summary>
    /// Encrypts plaintext using Data Protection API
    /// </summary>
    /// <param name="plaintext">The plaintext to encrypt</param>
    /// <param name="purpose">Optional purpose for key isolation (e.g., "OAuthTokens")</param>
    /// <returns>Base64-encoded encrypted string</returns>
    Task<string> EncryptAsync(string plaintext, string purpose = "default");

    /// <summary>
    /// Decrypts ciphertext using Data Protection API
    /// </summary>
    /// <param name="ciphertext">Base64-encoded encrypted string</param>
    /// <param name="purpose">Optional purpose for key isolation (must match encryption)</param>
    /// <returns>Decrypted plaintext</returns>
    Task<string> DecryptAsync(string ciphertext, string purpose = "default");
}
