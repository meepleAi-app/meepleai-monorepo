using System.Security.Cryptography;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.Authentication;
using Api.Observability;
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
    private readonly AuditService _auditService;
    private readonly IPasswordHashingService _passwordHashingService;
    private readonly IRateLimitService _rateLimitService;
    private readonly IAlertingService _alertingService;
    private readonly StackExchange.Redis.IConnectionMultiplexer _redis;
    private readonly ILogger<TotpService> _logger;
    private readonly TimeProvider _timeProvider;

    private const int SecretSizeBytes = 20; // 160 bits (TOTP standard)
    private const int BackupCodeCount = 10;
    private const int BackupCodeLength = 8;
    private const string TotpIssuer = "MeepleAI";
    private const int TimeStepSeconds = 30; // TOTP time step (standard)
    private const int TimeWindowSteps = 2; // Allow ±2 steps (60 second window)
    private const int PbkdfIterations = 210_000; // Same as password hashing

    // SEC-05: Rate limiting constants for brute force protection (Issue #576)
    private const int MaxTotpAttempts = 5; // Maximum attempts
    private const double TotpRateLimitWindowSeconds = 300; // 5 minutes window
    private const int LockoutDurationMinutes = 15; // Account lockout duration
    private const int AlertThresholdAttempts = 10; // Alert after N+ failures

    public TotpService(
        MeepleAiDbContext dbContext,
        IEncryptionService encryptionService,
        AuditService auditService,
        IPasswordHashingService passwordHashingService,
        IRateLimitService rateLimitService,
        IAlertingService alertingService,
        StackExchange.Redis.IConnectionMultiplexer redis,
        ILogger<TotpService> logger,
        TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext;
        _encryptionService = encryptionService;
        _auditService = auditService;
        _passwordHashingService = passwordHashingService;
        _rateLimitService = rateLimitService;
        _alertingService = alertingService;
        _redis = redis;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Generate TOTP setup with secret, QR code, and backup codes
    /// </summary>
    public async Task<TotpSetupResponse> GenerateSetupAsync(Guid userId, string userEmail, CancellationToken cancellationToken = default)
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
                Id = Guid.NewGuid(),
                UserId = userId,
                CodeHash = codeHash,
                IsUsed = false,
                CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
            });
        }
        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(userId.ToString(), "TwoFactorSetup", "TwoFactor", userId.ToString(), "Success",
            "User generated 2FA setup");
        _logger.LogInformation("2FA setup generated for user {UserId}", userId);

        // SEC-08: Track 2FA setup operation
        MeepleAiMetrics.Record2FALifecycle("setup", userId: userId.ToString());

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
    public async Task<bool> EnableTwoFactorAsync(Guid userId, string totpCode, CancellationToken cancellationToken = default)
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
        var isValid = VerifyTotpCode(secret, totpCode, out _); // Discard timeStep during setup

        if (!isValid)
        {
            _logger.LogWarning("2FA enable failed: Invalid code for user {UserId}", userId);
            await _auditService.LogAsync(userId.ToString(), "TwoFactorEnable", "TwoFactor", userId.ToString(), "Failed",
                "Invalid verification code");
            return false;
        }

        // Enable 2FA
        user.IsTwoFactorEnabled = true;
        user.TwoFactorEnabledAt = _timeProvider.GetUtcNow().UtcDateTime;
        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(userId.ToString(), "TwoFactorEnable", "TwoFactor", userId.ToString(), "Success",
            "User enabled 2FA");
        _logger.LogInformation("2FA enabled for user {UserId}", userId);

        // SEC-08: Track 2FA enable operation
        MeepleAiMetrics.Record2FALifecycle("enable", userId: userId.ToString());

        return true;
    }

    /// <summary>
    /// Verify TOTP code during login with comprehensive multi-layer security
    /// SEC-05 (Issue #576): Brute force protection - rate limiting + lockout + alerting
    /// SEC-07 (Issue #1787): Replay attack prevention - nonce validation
    /// SEC-08 (Issue #1788): Security monitoring - Prometheus metrics
    /// </summary>
    public async Task<bool> VerifyCodeAsync(Guid userId, string code, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FindAsync(userId);
        if (user == null || !user.IsTwoFactorEnabled || string.IsNullOrEmpty(user.TotpSecretEncrypted))
        {
            _logger.LogWarning("2FA verify failed: Invalid user state for {UserId}", userId);
            return false;
        }

        // SEC-05: Rate limiting check (5 attempts per 5 minutes) - LAYER 1
        var rateLimitKey = $"2fa:totp:{userId}";
        var rateLimitResult = await _rateLimitService.CheckRateLimitAsync(
            rateLimitKey,
            MaxTotpAttempts,
            MaxTotpAttempts / TotpRateLimitWindowSeconds,
            cancellationToken);

        if (!rateLimitResult.Allowed)
        {
            _logger.LogWarning("🚨 2FA rate limit exceeded for user {UserId}. Retry after {RetryAfter}s",
                userId, rateLimitResult.RetryAfterSeconds);

            await _auditService.LogAsync(userId.ToString(), "TotpRateLimitExceeded", "TwoFactor", userId.ToString(), "Blocked",
                $"Rate limit exceeded. Retry after {rateLimitResult.RetryAfterSeconds}s");

            await CheckAndTriggerSecurityAlertAsync(userId, "TOTP", cancellationToken);
            return false;
        }

        // SEC-05: Account lockout check (5 failures = 15min lockout) - LAYER 2
        var isLockedOut = await IsAccountLockedOutAsync(userId, "totp", cancellationToken);
        if (isLockedOut)
        {
            _logger.LogWarning("🔒 Account locked out for user {UserId} due to excessive failed 2FA attempts", userId);
            await _auditService.LogAsync(userId.ToString(), "TotpAccountLockedOut", "TwoFactor", userId.ToString(), "Blocked",
                $"Account locked for {LockoutDurationMinutes} minutes");
            return false;
        }

        // SEC-07: Replay attack prevention (Issue #1787) - LAYER 3
        var codeHash = _passwordHashingService.HashSecret(code);
        var alreadyUsed = await _dbContext.UsedTotpCodes
            .Where(u => u.UserId == userId &&
                        u.CodeHash == codeHash &&
                        u.ExpiresAt > _timeProvider.GetUtcNow().UtcDateTime)
            .AnyAsync(cancellationToken);

        if (alreadyUsed)
        {
            _logger.LogWarning("🔴 TOTP replay attack detected for user {UserId}", userId);
            await _auditService.LogAsync(userId.ToString(), "TotpReplayAttempt", "TwoFactor", userId.ToString(), "Blocked",
                "Replay attack prevented - code already used");

            // SEC-08: Track replay attack for security monitoring
            MeepleAiMetrics.Record2FAVerification("totp", success: false, userId: userId.ToString(), isReplayAttack: true);
            return false;
        }

        // Decrypt secret and verify code
        var secret = await _encryptionService.DecryptAsync(user.TotpSecretEncrypted, purpose: "TotpSecrets");
        var isValid = VerifyTotpCode(secret, code, out long timeStep);

        if (!isValid)
        {
            _logger.LogWarning("2FA verify failed: Invalid TOTP code for user {UserId}", userId);
            await _auditService.LogAsync(userId.ToString(), "TwoFactorVerify", "TwoFactor", userId.ToString(), "Failed",
                "Failed TOTP code attempt");

            // SEC-05: Track failed attempt for lockout
            await TrackFailedAttemptAsync(userId, "totp", cancellationToken);
            await CheckAndTriggerSecurityAlertAsync(userId, "TOTP", cancellationToken);

            // SEC-08: Track failed TOTP attempt
            MeepleAiMetrics.Record2FAVerification("totp", success: false, userId: userId.ToString());
        }
        else
        {
            // SEC-05: Clear failed attempts on success
            await ClearFailedAttemptsAsync(userId, "totp", cancellationToken);

            // SEC-07: Store used code to prevent replay (Issue #1787)
            var expiresAt = _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(2);
            await _dbContext.UsedTotpCodes.AddAsync(new UsedTotpCodeEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CodeHash = codeHash,
                TimeStep = timeStep,
                UsedAt = _timeProvider.GetUtcNow().UtcDateTime,
                ExpiresAt = expiresAt
            }, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("2FA verify success for user {UserId}, code stored for replay prevention", userId);

            // SEC-08: Track successful TOTP verification
            MeepleAiMetrics.Record2FAVerification("totp", success: true, userId: userId.ToString());
        }

        return isValid;
    }

    /// <summary>
    /// Verify backup code and mark as used (single-use enforcement)
    /// SECURITY FIX: Uses transaction with Serializable isolation to prevent race conditions
    /// SEC-05 (Issue #576): Brute force protection with rate limiting and alerting
    /// </summary>
    public async Task<bool> VerifyBackupCodeAsync(Guid userId, string backupCode, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FindAsync(userId);
        if (user == null || !user.IsTwoFactorEnabled)
        {
            _logger.LogWarning("Backup code verify failed: Invalid user state for {UserId}", userId);
            return false;
        }

        // SEC-05: Rate limiting check (5 attempts per 5 minutes) - same as TOTP
        var rateLimitKey = $"2fa:backup:{userId}";
        var rateLimitResult = await _rateLimitService.CheckRateLimitAsync(
            rateLimitKey,
            MaxTotpAttempts,
            MaxTotpAttempts / TotpRateLimitWindowSeconds,
            cancellationToken);

        if (!rateLimitResult.Allowed)
        {
            _logger.LogWarning("🚨 Backup code rate limit exceeded for user {UserId}. Retry after {RetryAfter}s",
                userId, rateLimitResult.RetryAfterSeconds);

            await _auditService.LogAsync(userId.ToString(), "BackupRateLimitExceeded", "TwoFactor", userId.ToString(), "Blocked",
                $"Rate limit exceeded. Retry after {rateLimitResult.RetryAfterSeconds}s");

            // Check if we should trigger security alert
            await CheckAndTriggerSecurityAlertAsync(userId, "BackupCode", cancellationToken);

            return false;
        }

        // Check for account lockout (separate from rate limiting)
        var isLockedOut = await IsAccountLockedOutAsync(userId, "backup", cancellationToken);
        if (isLockedOut)
        {
            _logger.LogWarning("🔒 Account locked out for user {UserId} due to excessive failed backup code attempts", userId);
            await _auditService.LogAsync(userId.ToString(), "BackupAccountLockedOut", "TwoFactor", userId.ToString(), "Blocked",
                $"Account locked for {LockoutDurationMinutes} minutes");
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
                await _auditService.LogAsync(userId.ToString(), "BackupCodeVerify", "TwoFactor", userId.ToString(), "Failed",
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
                    await _auditService.LogAsync(userId.ToString(), "BackupCodeUsed", "TwoFactor", userId.ToString(), "Success",
                        $"User authenticated with backup code ({remainingCodes} remaining)");
                    _logger.LogInformation("Backup code used for user {UserId}, {Remaining} codes remaining",
                        userId, remainingCodes);

                    // Warn if low on backup codes
                    if (remainingCodes < 3)
                    {
                        _logger.LogWarning("User {UserId} has only {Remaining} backup codes remaining",
                            userId, remainingCodes);
                    }

                    // SEC-05: Clear failed attempts on successful verification
                    await ClearFailedAttemptsAsync(userId, "backup", cancellationToken);

                    // SEC-08: Track successful backup code use
                    MeepleAiMetrics.Record2FAVerification("backup_code", success: true, userId: userId.ToString());

                    return true;
                }
            }

            // No match found
            _logger.LogWarning("Backup code verify failed: Invalid code for user {UserId}", userId);
            await _auditService.LogAsync(userId.ToString(), "BackupCodeVerify", "TwoFactor", userId.ToString(), "Failed",
                "Failed backup code attempt");

            // SEC-05: Track failed attempt for lockout mechanism
            await TrackFailedAttemptAsync(userId, "backup", cancellationToken);
            await CheckAndTriggerSecurityAlertAsync(userId, "BackupCode", cancellationToken);

            // SEC-08: Track failed backup code attempt
            MeepleAiMetrics.Record2FAVerification("backup_code", success: false, userId: userId.ToString());

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
    public async Task DisableTwoFactorAsync(Guid userId, string password, string totpOrBackupCode, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FindAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("2FA disable failed: User {UserId} not found", userId);
            throw new InvalidOperationException("User not found");
        }

        // OAuth-only users don't have a password - require OAuth re-authentication
        if (user.PasswordHash == null)
        {
            _logger.LogWarning("2FA disable failed: OAuth-only user {UserId} cannot use password auth", userId);
            await _auditService.LogAsync(userId.ToString(), "TwoFactorDisable", "TwoFactor", userId.ToString(), "Failed",
                "OAuth user - password authentication not available");
            throw new UnauthorizedAccessException("OAuth users must re-authenticate via OAuth provider");
        }

        // Verify password first (using private method, need to duplicate logic)
        var isPasswordValid = VerifyPassword(user.PasswordHash, password);
        if (!isPasswordValid)
        {
            _logger.LogWarning("2FA disable failed: Invalid password for user {UserId}", userId);
            await _auditService.LogAsync(userId.ToString(), "TwoFactorDisable", "TwoFactor", userId.ToString(), "Failed",
                "Invalid password");
            throw new UnauthorizedAccessException("Invalid password");
        }

        // Verify TOTP code or backup code
        var isTotpValid = await VerifyCodeAsync(userId, totpOrBackupCode);
        var isBackupValid = !isTotpValid && await VerifyBackupCodeAsync(userId, totpOrBackupCode);

        if (!isTotpValid && !isBackupValid)
        {
            _logger.LogWarning("2FA disable failed: Invalid verification code for user {UserId}", userId);
            await _auditService.LogAsync(userId.ToString(), "TwoFactorDisable", "TwoFactor", userId.ToString(), "Failed",
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

        await _auditService.LogAsync(userId.ToString(), "TwoFactorDisable", "TwoFactor", userId.ToString(), "Success",
            "User disabled 2FA");
        _logger.LogInformation("2FA disabled for user {UserId}", userId);

        // SEC-08: Track 2FA disable operation
        MeepleAiMetrics.Record2FALifecycle("disable", userId: userId.ToString());
    }

    /// <summary>
    /// Get 2FA status for user
    /// </summary>
    public async Task<TwoFactorStatusResponse> GetTwoFactorStatusAsync(Guid userId, CancellationToken cancellationToken = default)
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
    /// SECURITY: Issue #1787 - Returns timeStep for nonce tracking
    /// </summary>
    private bool VerifyTotpCode(string secret, string code, out long timeStep)
    {
        timeStep = 0;
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
                timeStep = timeStepMatched;
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
    /// Hash backup code with PBKDF2 using centralized IPasswordHashingService
    /// </summary>
    private string HashBackupCode(string code)
    {
        return _passwordHashingService.HashSecret(code);
    }

    /// <summary>
    /// Verify backup code against hash using centralized IPasswordHashingService
    /// </summary>
    private bool VerifyBackupCode(string encodedHash, string code)
    {
        return _passwordHashingService.VerifySecret(code, encodedHash);
    }

    /// <summary>
    /// Verify password against hash using centralized IPasswordHashingService
    /// </summary>
    private bool VerifyPassword(string encodedHash, string password)
    {
        return _passwordHashingService.VerifySecret(password, encodedHash);
    }

    // ========================================
    // SEC-05 (Issue #576): Brute Force Protection Helpers
    // ========================================

    /// <summary>
    /// Track failed 2FA attempt in Redis for lockout mechanism.
    /// Pattern: Sliding window counter with 15-minute TTL.
    /// </summary>
    private async Task TrackFailedAttemptAsync(Guid userId, string attemptType, CancellationToken cancellationToken)
    {
        var redisKey = $"2fa:failed:{attemptType}:{userId}";
        var redisDb = _redis.GetDatabase();
        var lockoutExpiry = TimeSpan.FromMinutes(LockoutDurationMinutes);

        // Increment counter with sliding window expiry
        await redisDb.StringIncrementAsync(redisKey);
        await redisDb.KeyExpireAsync(redisKey, lockoutExpiry);

        _logger.LogDebug("Tracked failed {AttemptType} attempt for user {UserId}", attemptType, userId);
    }

    /// <summary>
    /// Check if account is locked out due to excessive failed attempts.
    /// Lockout triggers after 5 failed attempts within 15-minute window.
    /// </summary>
    private async Task<bool> IsAccountLockedOutAsync(Guid userId, string attemptType, CancellationToken cancellationToken)
    {
        var redisKey = $"2fa:failed:{attemptType}:{userId}";
        var redisDb = _redis.GetDatabase();
        var failedAttempts = (int)await redisDb.StringGetAsync(redisKey);

        var isLockedOut = failedAttempts >= MaxTotpAttempts;
        if (isLockedOut)
        {
            _logger.LogWarning("Account locked: {FailedAttempts} failed {AttemptType} attempts for user {UserId}",
                failedAttempts, attemptType, userId);
        }

        return isLockedOut;
    }

    /// <summary>
    /// Clear failed attempt counter after successful verification.
    /// </summary>
    private async Task ClearFailedAttemptsAsync(Guid userId, string attemptType, CancellationToken cancellationToken)
    {
        var redisKey = $"2fa:failed:{attemptType}:{userId}";
        var redisDb = _redis.GetDatabase();
        await redisDb.KeyDeleteAsync(redisKey);

        _logger.LogDebug("Cleared failed {AttemptType} attempts for user {UserId}", attemptType, userId);
    }

    /// <summary>
    /// Check if security alert threshold reached and trigger alert.
    /// Alerts on 10+ failed attempts to detect sophisticated attacks.
    /// </summary>
    private async Task CheckAndTriggerSecurityAlertAsync(Guid userId, string attemptType, CancellationToken cancellationToken)
    {
        var redisKey = $"2fa:failed:{attemptType.ToLowerInvariant()}:{userId}";
        var redisDb = _redis.GetDatabase();
        var failedAttempts = (int)await redisDb.StringGetAsync(redisKey);

        if (failedAttempts >= AlertThresholdAttempts)
        {
            _logger.LogWarning("🚨 SECURITY ALERT: {FailedAttempts} failed {AttemptType} attempts for user {UserId}",
                failedAttempts, attemptType, userId);

            await _alertingService.SendAlertAsync(
                alertType: $"2FA_BRUTE_FORCE_{attemptType.ToUpperInvariant()}",
                severity: "critical",
                message: $"Potential brute force attack detected: {failedAttempts} failed {attemptType} attempts for user {userId}",
                metadata: new Dictionary<string, object>
                {
                    ["user_id"] = userId.ToString(),
                    ["attempt_type"] = attemptType,
                    ["failed_attempts"] = failedAttempts,
                    ["threshold"] = AlertThresholdAttempts,
                    ["lockout_active"] = failedAttempts >= MaxTotpAttempts
                },
                cancellationToken: cancellationToken);
        }
    }
}