using System.Security.Cryptography;
using Api.Helpers;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

/// <summary>
/// Service for email verification operations.
/// ISSUE-3071: Email verification backend implementation.
/// </summary>
internal class EmailVerificationService : IEmailVerificationService
{
    private const int TokenSize = 32;
    private const int TokenExpiryHours = 24;
    private const int ResendRateLimitPerMinute = 1;

    private readonly MeepleAiDbContext _db;
    private readonly IEmailService _emailService;
    private readonly IRateLimitService _rateLimitService;
    private readonly ILogger<EmailVerificationService> _logger;
    private readonly TimeProvider _timeProvider;

    public EmailVerificationService(
        MeepleAiDbContext db,
        IEmailService emailService,
        IRateLimitService rateLimitService,
        ILogger<EmailVerificationService> logger,
        TimeProvider? timeProvider = null)
    {
        _db = db;
        _emailService = emailService;
        _rateLimitService = rateLimitService;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<bool> SendVerificationEmailAsync(
        Guid userId,
        string email,
        string displayName,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(email);

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // Find user
        var user = await _db.Users.FindAsync([userId], ct).ConfigureAwait(false);
        if (user == null)
        {
            _logger.LogWarning("Attempted to send verification email to non-existent user: {UserId}", userId);
            return false;
        }

        // Check if already verified
        if (user.EmailVerified)
        {
            _logger.LogInformation("User {UserId} already has verified email", userId);
            return true;
        }

        // Invalidate any existing active tokens for this user
        // Use InvalidatedAt (not VerifiedAt) to distinguish superseded tokens from verified tokens
        var existingTokens = await _db.EmailVerifications
            .Where(t => t.UserId == userId && t.VerifiedAt == null && t.InvalidatedAt == null && t.ExpiresAt > now)
            .ToListAsync(ct).ConfigureAwait(false);

        foreach (var token in existingTokens)
        {
            token.InvalidatedAt = now; // Mark as superseded (distinct from verified)
        }

        // Generate new token
        var tokenBytes = RandomNumberGenerator.GetBytes(TokenSize);
        var verificationTokenValue = Convert.ToBase64String(tokenBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");

        var tokenHash = HashToken(verificationTokenValue);

        var verification = new EmailVerificationEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = now.AddHours(TokenExpiryHours),
            CreatedAt = now
        };

        _db.EmailVerifications.Add(verification);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        // Send email
        try
        {
            await _emailService.SendVerificationEmailAsync(
                normalizedEmail,
                displayName ?? normalizedEmail,
                verificationTokenValue,
                ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Verification email sent to user: {UserId}",
                userId);

            return true;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
#pragma warning restore CA1031
        {
            // RESILIENCE PATTERN: Email failures should not fail the registration flow
            // We log the error and the user can request a resend later
            _logger.LogError(
                ex,
                "Failed to send verification email to user: {UserId}",
                userId);
            return false;
        }
    }

    public async Task<bool> VerifyEmailAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var tokenHash = HashToken(token);

        var verification = await _db.EmailVerifications
            .Include(v => v.User)
            .FirstOrDefaultAsync(v => v.TokenHash == tokenHash, ct).ConfigureAwait(false);

        if (verification == null)
        {
            _logger.LogWarning("Invalid email verification token");
            return false;
        }

        if (verification.VerifiedAt != null)
        {
            _logger.LogWarning(
                "Attempt to reuse email verification token: {TokenId}",
                verification.Id);
            return false;
        }

        if (verification.InvalidatedAt != null)
        {
            _logger.LogWarning(
                "Attempt to use invalidated/superseded email verification token: {TokenId}",
                verification.Id);
            return false;
        }

        if (verification.ExpiresAt <= now)
        {
            _logger.LogInformation(
                "Expired email verification token: {TokenId}",
                verification.Id);
            return false;
        }

        // Mark token as used and verify user's email
        verification.VerifiedAt = now;

        var user = verification.User;
        if (user == null)
        {
            _logger.LogError(
                "User not found for email verification token: {TokenId}",
                verification.Id);
            return false;
        }

        user.EmailVerified = true;
        user.EmailVerifiedAt = now;

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation(
            "Email verified for user: {UserId}",
            user.Id);

        return true;
    }

    public async Task<bool> ResendVerificationEmailAsync(string email, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email is required", nameof(email));
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();

        // Rate limiting check (1 per minute)
        var rateLimitKey = $"email_verification_resend:{normalizedEmail}";
        var rateLimitResult = await _rateLimitService.CheckRateLimitAsync(
            rateLimitKey,
            maxTokens: ResendRateLimitPerMinute,
            refillRate: ResendRateLimitPerMinute / 60.0, // Refill over 1 minute
            ct).ConfigureAwait(false);

        if (!rateLimitResult.Allowed)
        {
            _logger.LogWarning(
                "Email verification resend rate limit exceeded for email: {Email}. Retry after {RetryAfter}s",
                DataMasking.MaskEmail(normalizedEmail),
                rateLimitResult.RetryAfterSeconds);
            throw new InvalidOperationException("Too many verification requests. Please try again later.");
        }

        // Find user by email
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct).ConfigureAwait(false);

        // For security, always return success even if user doesn't exist
        // This prevents email enumeration attacks
        if (user == null)
        {
            _logger.LogInformation("Verification resend requested for non-existent email");
            return true;
        }

        // If already verified, return success silently
        if (user.EmailVerified)
        {
            _logger.LogInformation("Verification resend requested for already verified user: {UserId}", user.Id);
            return true;
        }

        // Send new verification email
        return await SendVerificationEmailAsync(
            user.Id,
            user.Email,
            user.DisplayName ?? user.Email,
            ct).ConfigureAwait(false);
    }

    public async Task<bool> IsEmailVerifiedAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.EmailVerified)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);

        return user;
    }

    private static string HashToken(string token)
    {
        return CryptographyHelper.ComputeSha256HashBase64(token);
    }
}
