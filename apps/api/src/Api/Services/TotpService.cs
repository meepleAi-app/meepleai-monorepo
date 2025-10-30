using System.Security.Cryptography;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using OtpNet;

namespace Api.Services;

/// <summary>
/// Service for TOTP-based two-factor authentication
/// AUTH-07: Implements TOTP generation, verification, and backup codes
/// </summary>
public class TotpService : ITotpService
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly IEncryptionService _encryptionService;
    private readonly AuthService _authService;
    private readonly AuditService _auditService;
    private readonly ILogger<TotpService> _logger;
    private readonly TimeProvider _timeProvider;

    private const int SecretSizeBytes = 20; // 160 bits (TOTP standard)
    private const int BackupCodeCount = 10;
    private const int BackupCodeLength = 8;
    private const string TotpIssuer = "MeepleAI";
    private const int TimeStepSeconds = 30; // TOTP time step (standard)
    private const int TimeWindowSteps = 2; // Allow ±2 steps (60 second window)
    private const int PbkdfIterations = 210_000; // Same as password hashing

    public TotpService(
        MeepleAiDbContext dbContext,
        IEncryptionService encryptionService,
        AuthService authService,
        AuditService auditService,
        ILogger<TotpService> logger,
        TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext;
        _encryptionService = encryptionService;
        _authService = authService;
        _auditService = auditService;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Generate TOTP setup with secret, QR code, and backup codes
    /// </summary>
    public async Task<TotpSetupResponse> GenerateSetupAsync(string userId, string userEmail)
    {
        var user = await _dbContext.Users.FindAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("2FA setup failed: User {UserId} not found", userId);
            throw new InvalidOperationException("User not found");
        }

        // Generate TOTP secret (160-bit)
        var secret = GenerateSecret();
        var encryptedSecret = await _encryptionService.EncryptAsync(secret, purpose: "TotpSecrets");

        // Generate QR code URL (otpauth:// URI format for authenticator apps)
        var qrCodeUrl = GenerateQrCodeUrl(userEmail, secret);

        // Generate backup codes
        var backupCodes = GenerateBackupCodes();

        // Store encrypted secret (not enabled yet - requires verification)
        user.TotpSecretEncrypted = encryptedSecret;
        user.IsTwoFactorEnabled = false; // Not enabled until verified
        await _dbContext.SaveChangesAsync();

        // Delete existing backup codes if re-enrolling
        var existingCodes = await _dbContext.UserBackupCodes
            .Where(bc => bc.UserId == userId)
            .ToListAsync();
        if (existingCodes.Any())
        {
            _dbContext.UserBackupCodes.RemoveRange(existingCodes);
        }

        // Store backup codes (hashed with PBKDF2, same security as passwords)
        foreach (var code in backupCodes)
        {
            var codeHash = HashBackupCode(code);
            _dbContext.UserBackupCodes.Add(new UserBackupCodeEntity
            {
                Id = Guid.NewGuid().ToString(),
                UserId = userId,
                CodeHash = codeHash,
                IsUsed = false,
                CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
            });
        }
        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(userId, "TwoFactorSetup", "TwoFactor", userId, "Success",
            "User generated 2FA setup");
        _logger.LogInformation("2FA setup generated for user {UserId}", userId);

        return new TotpSetupResponse
        {
            Secret = secret, // Only shown once during setup
            QrCodeUrl = qrCodeUrl,
            BackupCodes = backupCodes // Only shown once during setup
        };
    }

    /// <summary>
    /// Enable 2FA after verifying TOTP code (prevents misconfiguration)
    /// </summary>
    public async Task<bool> EnableTwoFactorAsync(string userId, string totpCode)
    {
        var user = await _dbContext.Users.FindAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("2FA enable failed: User {UserId} not found", userId);
            return false;
        }

        if (string.IsNullOrEmpty(user.TotpSecretEncrypted))
        {
            _logger.LogWarning("2FA enable failed: No secret configured for user {UserId}", userId);
            return false;
        }

        // Decrypt secret and verify code
        var secret = await _encryptionService.DecryptAsync(user.TotpSecretEncrypted, purpose: "TotpSecrets");
        var isValid = VerifyTotpCode(secret, totpCode);

        if (!isValid)
        {
            _logger.LogWarning("2FA enable failed: Invalid code for user {UserId}", userId);
            await _auditService.LogAsync(userId, "TwoFactorEnable", "TwoFactor", userId, "Failed",
                "Invalid verification code");
            return false;
        }

        // Enable 2FA
        user.IsTwoFactorEnabled = true;
        user.TwoFactorEnabledAt = _timeProvider.GetUtcNow().UtcDateTime;
        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(userId, "TwoFactorEnable", "TwoFactor", userId, "Success",
            "User enabled 2FA");
        _logger.LogInformation("2FA enabled for user {UserId}", userId);

        return true;
    }

    /// <summary>
    /// Verify TOTP code during login
    /// </summary>
    public async Task<bool> VerifyCodeAsync(string userId, string code)
    {
        var user = await _dbContext.Users.FindAsync(userId);
        if (user == null || !user.IsTwoFactorEnabled || string.IsNullOrEmpty(user.TotpSecretEncrypted))
        {
            _logger.LogWarning("2FA verify failed: Invalid user state for {UserId}", userId);
            return false;
        }

        // Decrypt secret and verify code
        var secret = await _encryptionService.DecryptAsync(user.TotpSecretEncrypted, purpose: "TotpSecrets");
        var isValid = VerifyTotpCode(secret, code);

        if (!isValid)
        {
            _logger.LogWarning("2FA verify failed: Invalid TOTP code for user {UserId}", userId);
            await _auditService.LogAsync(userId, "TwoFactorVerify", "TwoFactor", userId, "Failed",
                "Failed TOTP code attempt");
        }
        else
        {
            _logger.LogInformation("2FA verify success for user {UserId}", userId);
        }

        return isValid;
    }

    /// <summary>
    /// Verify backup code and mark as used (single-use enforcement)
    /// SECURITY FIX: Uses transaction with Serializable isolation to prevent race conditions
    /// </summary>
    public async Task<bool> VerifyBackupCodeAsync(string userId, string backupCode)
    {
        var user = await _dbContext.Users.FindAsync(userId);
        if (user == null || !user.IsTwoFactorEnabled)
        {
            _logger.LogWarning("Backup code verify failed: Invalid user state for {UserId}", userId);
            return false;
        }

        // Use transaction with Serializable isolation to prevent concurrent use of same code
        using var transaction = await _dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        try
        {
            // Get all unused backup codes for user (within transaction)
            var backupCodes = await _dbContext.UserBackupCodes
                .Where(bc => bc.UserId == userId && !bc.IsUsed)
                .ToListAsync();

            if (!backupCodes.Any())
            {
                _logger.LogWarning("Backup code verify failed: No unused codes for user {UserId}", userId);
                await _auditService.LogAsync(userId, "BackupCodeVerify", "TwoFactor", userId, "Failed",
                    "No unused backup codes available");
                return false;
            }

            // Verify code against hashed codes (constant-time comparison via PBKDF2)
            foreach (var storedCode in backupCodes)
            {
                var isMatch = VerifyBackupCode(storedCode.CodeHash, backupCode);
                if (isMatch)
                {
                    // Mark as used (atomic with transaction commit)
                    storedCode.IsUsed = true;
                    storedCode.UsedAt = _timeProvider.GetUtcNow().UtcDateTime;
                    await _dbContext.SaveChangesAsync();
                    await transaction.CommitAsync();

                    var remainingCodes = backupCodes.Count - 1;
                    await _auditService.LogAsync(userId, "BackupCodeUsed", "TwoFactor", userId, "Success",
                        $"User authenticated with backup code ({remainingCodes} remaining)");
                    _logger.LogInformation("Backup code used for user {UserId}, {Remaining} codes remaining",
                        userId, remainingCodes);

                    // Warn if low on backup codes
                    if (remainingCodes < 3)
                    {
                        _logger.LogWarning("User {UserId} has only {Remaining} backup codes remaining",
                            userId, remainingCodes);
                    }

                    return true;
                }
            }

            // No match found
            _logger.LogWarning("Backup code verify failed: Invalid code for user {UserId}", userId);
            await _auditService.LogAsync(userId, "BackupCodeVerify", "TwoFactor", userId, "Failed",
                "Failed backup code attempt");
            return false;
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error during backup code verification for user {UserId}", userId);
            await transaction.RollbackAsync();
            throw new InvalidOperationException("Failed to verify backup code due to database error", ex);
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(ex, "Cryptographic error during backup code verification for user {UserId}", userId);
            await transaction.RollbackAsync();
            throw new InvalidOperationException("Failed to verify backup code due to cryptographic error", ex);
        }
    }

    /// <summary>
    /// Disable 2FA with password and code verification
    /// </summary>
    public async Task DisableTwoFactorAsync(string userId, string password, string totpOrBackupCode)
    {
        var user = await _dbContext.Users.FindAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("2FA disable failed: User {UserId} not found", userId);
            throw new InvalidOperationException("User not found");
        }

        // Verify password first (using private method, need to duplicate logic)
        var isPasswordValid = VerifyPassword(user.PasswordHash, password);
        if (!isPasswordValid)
        {
            _logger.LogWarning("2FA disable failed: Invalid password for user {UserId}", userId);
            await _auditService.LogAsync(userId, "TwoFactorDisable", "TwoFactor", userId, "Failed",
                "Invalid password");
            throw new UnauthorizedAccessException("Invalid password");
        }

        // Verify TOTP code or backup code
        var isTotpValid = await VerifyCodeAsync(userId, totpOrBackupCode);
        var isBackupValid = !isTotpValid && await VerifyBackupCodeAsync(userId, totpOrBackupCode);

        if (!isTotpValid && !isBackupValid)
        {
            _logger.LogWarning("2FA disable failed: Invalid verification code for user {UserId}", userId);
            await _auditService.LogAsync(userId, "TwoFactorDisable", "TwoFactor", userId, "Failed",
                "Invalid verification code");
            throw new UnauthorizedAccessException("Invalid verification code");
        }

        // Disable 2FA and clear all data
        user.IsTwoFactorEnabled = false;
        user.TotpSecretEncrypted = null;
        user.TwoFactorEnabledAt = null;

        // Delete all backup codes (used and unused)
        var allBackupCodes = await _dbContext.UserBackupCodes
            .Where(bc => bc.UserId == userId)
            .ToListAsync();
        _dbContext.UserBackupCodes.RemoveRange(allBackupCodes);

        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(userId, "TwoFactorDisable", "TwoFactor", userId, "Success",
            "User disabled 2FA");
        _logger.LogInformation("2FA disabled for user {UserId}", userId);
    }

    /// <summary>
    /// Get 2FA status for user
    /// </summary>
    public async Task<TwoFactorStatusResponse> GetTwoFactorStatusAsync(string userId)
    {
        var user = await _dbContext.Users.FindAsync(userId);
        if (user == null)
        {
            throw new InvalidOperationException("User not found");
        }

        var unusedBackupCodes = await _dbContext.UserBackupCodes
            .Where(bc => bc.UserId == userId && !bc.IsUsed)
            .CountAsync();

        return new TwoFactorStatusResponse
        {
            IsEnabled = user.IsTwoFactorEnabled,
            EnabledAt = user.TwoFactorEnabledAt,
            UnusedBackupCodesCount = unusedBackupCodes
        };
    }

    // ==================== PRIVATE HELPER METHODS ====================

    /// <summary>
    /// Generate cryptographically secure 160-bit TOTP secret
    /// </summary>
    private string GenerateSecret()
    {
        var key = KeyGeneration.GenerateRandomKey(SecretSizeBytes);
        return Base32Encoding.ToString(key);
    }

    /// <summary>
    /// Generate otpauth:// URL for QR code (compatible with all authenticator apps)
    /// </summary>
    private string GenerateQrCodeUrl(string userEmail, string secret)
    {
        var issuer = Uri.EscapeDataString(TotpIssuer);
        var email = Uri.EscapeDataString(userEmail);
        var secretParam = Uri.EscapeDataString(secret);

        // Format: otpauth://totp/ISSUER:EMAIL?secret=SECRET&issuer=ISSUER
        return $"otpauth://totp/{issuer}:{email}?secret={secretParam}&issuer={issuer}";
    }

    /// <summary>
    /// Verify TOTP code with time window (±2 steps = 60 seconds total)
    /// </summary>
    private bool VerifyTotpCode(string secret, string code)
    {
        try
        {
            var secretBytes = Base32Encoding.ToBytes(secret);
            var totp = new Totp(secretBytes, step: TimeStepSeconds);

            // Verify with time window for clock skew tolerance
            // VerificationWindow(previous: 2, future: 2) = ±60 seconds
            var isValid = totp.VerifyTotp(code, out long timeStepMatched,
                new VerificationWindow(previous: TimeWindowSteps, future: TimeWindowSteps));

            if (isValid)
            {
                _logger.LogDebug("TOTP code verified at time step {TimeStep}", timeStepMatched);
            }

            return isValid;
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Invalid TOTP secret or code format during verification");
            return false;
        }
        catch (FormatException ex)
        {
            _logger.LogError(ex, "TOTP code format error during verification");
            return false;
        }
    }

    /// <summary>
    /// Generate 10 secure backup codes (8 characters each, XXXX-XXXX format)
    /// </summary>
    private List<string> GenerateBackupCodes()
    {
        var codes = new List<string>();
        for (int i = 0; i < BackupCodeCount; i++)
        {
            codes.Add(GenerateRandomCode());
        }
        return codes;
    }

    /// <summary>
    /// Generate cryptographically secure random backup code
    /// Format: XXXX-XXXX (8 chars, no ambiguous characters like O/0, I/1, l/1)
    /// </summary>
    private string GenerateRandomCode()
    {
        // Remove ambiguous characters: O, 0, I, l, 1
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var codeChars = new char[BackupCodeLength];

        // Use cryptographically secure random
        using var rng = RandomNumberGenerator.Create();
        var randomBytes = new byte[BackupCodeLength];
        rng.GetBytes(randomBytes);

        for (int i = 0; i < BackupCodeLength; i++)
        {
            codeChars[i] = chars[randomBytes[i] % chars.Length];
        }

        var code = new string(codeChars);

        // Format as XXXX-XXXX for readability
        return $"{code.Substring(0, 4)}-{code.Substring(4, 4)}";
    }

    /// <summary>
    /// Hash backup code with PBKDF2 (same algorithm as passwords for consistency)
    /// </summary>
    private string HashBackupCode(string code)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(code, salt, PbkdfIterations, HashAlgorithmName.SHA256, 32);
        return $"v1.{PbkdfIterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    /// <summary>
    /// Verify backup code against hash (constant-time comparison)
    /// </summary>
    private bool VerifyBackupCode(string encodedHash, string code)
    {
        try
        {
            var parts = encodedHash.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 4 || parts[0] != "v1")
            {
                return false;
            }

            if (!int.TryParse(parts[1], out var iterations))
            {
                return false;
            }

            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);

            var hash = Rfc2898DeriveBytes.Pbkdf2(code, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(hash, expected);
        }
        catch (FormatException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    /// Verify password against hash (duplicated from AuthService since it's private)
    /// </summary>
    private bool VerifyPassword(string encodedHash, string password)
    {
        try
        {
            var parts = encodedHash.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 4 || parts[0] != "v1")
            {
                return false;
            }

            if (!int.TryParse(parts[1], out var iterations))
            {
                return false;
            }

            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);

            var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(hash, expected);
        }
        catch (FormatException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
