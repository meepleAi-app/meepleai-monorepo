using System.Security.Cryptography;
using System.Text;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

public class PasswordResetService : IPasswordResetService
{
    private const int TokenSize = 32;
    private const int TokenExpiryMinutes = 30;
    private const int RateLimitPerHour = 3;
    private readonly MeepleAiDbContext _db;
    private readonly IEmailService _emailService;
    private readonly IRateLimitService _rateLimitService;
    private readonly ILogger<PasswordResetService> _logger;
    private readonly TimeProvider _timeProvider;

    public PasswordResetService(
        MeepleAiDbContext db,
        IEmailService emailService,
        IRateLimitService rateLimitService,
        ILogger<PasswordResetService> logger,
        TimeProvider? timeProvider = null)
    {
        _db = db;
        _emailService = emailService;
        _rateLimitService = rateLimitService;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<bool> RequestPasswordResetAsync(string email, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email is required", nameof(email));
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // Rate limiting check
        var rateLimitKey = $"password_reset:{normalizedEmail}";
        var rateLimitResult = await _rateLimitService.CheckRateLimitAsync(
            rateLimitKey,
            maxTokens: RateLimitPerHour,
            refillRate: RateLimitPerHour / 3600.0, // Refill over 1 hour
            ct);

        if (!rateLimitResult.Allowed)
        {
            _logger.LogWarning(
                "Password reset rate limit exceeded for email: {Email}. Retry after {RetryAfter}s",
                normalizedEmail,
                rateLimitResult.RetryAfterSeconds);
            throw new InvalidOperationException("Too many password reset requests. Please try again later.");
        }

        // Find user by email
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);

        // For security, always return success even if user doesn't exist
        // This prevents email enumeration attacks
        if (user == null)
        {
            _logger.LogInformation(
                "Password reset requested for non-existent email: {Email}",
                normalizedEmail);
            return true;
        }

        // Invalidate any existing unused tokens for this user
        var existingTokens = await _db.PasswordResetTokens
            .Where(t => t.UserId == user.Id && !t.IsUsed && t.ExpiresAt > now)
            .ToListAsync(ct);

        foreach (var token in existingTokens)
        {
            token.IsUsed = true;
        }

        // Generate new token
        var tokenBytes = RandomNumberGenerator.GetBytes(TokenSize);
        var resetTokenValue = Convert.ToBase64String(tokenBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");

        var tokenHash = HashToken(resetTokenValue);

        var resetToken = new PasswordResetTokenEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            UserId = user.Id,
            TokenHash = tokenHash,
            ExpiresAt = now.AddMinutes(TokenExpiryMinutes),
            IsUsed = false,
            CreatedAt = now
        };

        _db.PasswordResetTokens.Add(resetToken);
        await _db.SaveChangesAsync(ct);

        // Send email
        try
        {
            await _emailService.SendPasswordResetEmailAsync(
                user.Email,
                user.DisplayName ?? user.Email,
                resetTokenValue,
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send password reset email to user: {UserId}",
                user.Id);
            // Don't throw - we don't want to expose whether the email was sent successfully
        }

        _logger.LogInformation(
            "Password reset token created for user: {UserId}",
            user.Id);

        return true;
    }

    public async Task<bool> ValidateResetTokenAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var tokenHash = HashToken(token);

        var resetToken = await _db.PasswordResetTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

        if (resetToken == null)
        {
            return false;
        }

        if (resetToken.IsUsed)
        {
            _logger.LogWarning(
                "Attempt to reuse password reset token: {TokenId}",
                resetToken.Id);
            return false;
        }

        if (resetToken.ExpiresAt <= now)
        {
            _logger.LogInformation(
                "Expired password reset token: {TokenId}",
                resetToken.Id);
            return false;
        }

        return true;
    }

    public async Task<(bool Success, string? UserId)> ResetPasswordAsync(
        string token,
        string newPassword,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("Token is required", nameof(token));
        }

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
        {
            throw new ArgumentException("Password must be at least 8 characters", nameof(newPassword));
        }

        // Validate password complexity
        if (!IsValidPassword(newPassword))
        {
            throw new ArgumentException(
                "Password must contain at least one uppercase letter, one lowercase letter, and one number",
                nameof(newPassword));
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var tokenHash = HashToken(token);

        var resetToken = await _db.PasswordResetTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

        if (resetToken == null)
        {
            _logger.LogWarning("Invalid password reset token");
            return (false, null);
        }

        if (resetToken.IsUsed)
        {
            _logger.LogWarning(
                "Attempt to reuse password reset token: {TokenId}",
                resetToken.Id);
            return (false, null);
        }

        if (resetToken.ExpiresAt <= now)
        {
            _logger.LogInformation(
                "Expired password reset token: {TokenId}",
                resetToken.Id);
            return (false, null);
        }

        // Mark token as used
        resetToken.IsUsed = true;

        // Update user password
        var user = resetToken.User;
        if (user == null)
        {
            _logger.LogError(
                "User not found for password reset token: {TokenId}",
                resetToken.Id);
            return (false, null);
        }

        user.PasswordHash = HashPassword(newPassword);

        // Revoke all existing sessions for security
        var existingSessions = await _db.UserSessions
            .Where(s => s.UserId == user.Id && s.RevokedAt == null)
            .ToListAsync(ct);

        foreach (var session in existingSessions)
        {
            session.RevokedAt = now;
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Password reset completed for user: {UserId}",
            user.Id);

        return (true, user.Id);
    }

    private static string HashToken(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }

    private static string HashPassword(string password)
    {
        const int iterations = 210_000;
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, 32);
        return $"v1.{iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    private static bool IsValidPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            return false;
        }

        var hasUpper = false;
        var hasLower = false;
        var hasNumber = false;

        foreach (var c in password)
        {
            if (char.IsUpper(c)) hasUpper = true;
            if (char.IsLower(c)) hasLower = true;
            if (char.IsDigit(c)) hasNumber = true;

            if (hasUpper && hasLower && hasNumber)
            {
                return true;
            }
        }

        return hasUpper && hasLower && hasNumber;
    }
}
