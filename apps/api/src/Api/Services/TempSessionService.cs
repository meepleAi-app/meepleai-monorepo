using System.Security.Cryptography;
using Api.Helpers;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

/// <summary>
/// Service for temporary 2FA sessions
/// AUTH-07: Manages short-lived, single-use tokens between password and 2FA verification
/// </summary>
public class TempSessionService : ITempSessionService
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<TempSessionService> _logger;
    private readonly TimeProvider _timeProvider;

    private const int TempSessionLifetimeMinutes = 5;
    private const int TokenSizeBytes = 32; // 256 bits

    public TempSessionService(
        MeepleAiDbContext dbContext, 
        ILogger<TempSessionService> logger,
        TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Create temp session with cryptographically secure token
    /// </summary>
    public async Task<string> CreateTempSessionAsync(Guid userId, string? ipAddress = null)
    {
        // Generate secure random token
        var token = GenerateSecureToken();
        var tokenHash = HashToken(token);

        var now = _timeProvider.GetUtcNow();
        var tempSession = new TempSessionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            IpAddress = ipAddress,
            CreatedAt = now.UtcDateTime,
            ExpiresAt = now.AddMinutes(TempSessionLifetimeMinutes).UtcDateTime,
            IsUsed = false
        };

        _dbContext.TempSessions.Add(tempSession);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Temp session created for user {UserId}, expires at {ExpiresAt}",
            userId, tempSession.ExpiresAt);

        return token;
    }

    /// <summary>
    /// Validate temp session and mark as used (single-use enforcement)
    /// </summary>
    public async Task<Guid?> ValidateAndConsumeTempSessionAsync(string token)
    {
        var tokenHash = HashToken(token);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // Find temp session with Serializable isolation (prevent concurrent use)
        using var transaction = await _dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        try
        {
            var tempSession = await _dbContext.TempSessions
                .FirstOrDefaultAsync(ts => ts.TokenHash == tokenHash && !ts.IsUsed && ts.ExpiresAt > now);

            if (tempSession == null)
            {
                _logger.LogWarning("Temp session validation failed: token not found, expired, or already used");
                return null;
            }

            // Mark as used (single-use enforcement)
            tempSession.IsUsed = true;
            tempSession.UsedAt = _timeProvider.GetUtcNow().UtcDateTime;
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Temp session validated and consumed for user {UserId}", tempSession.UserId);
            return tempSession.UserId;
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error during temp session validation");
            await transaction.RollbackAsync();
            return null;
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(ex, "Cryptographic error during temp session token validation");
            await transaction.RollbackAsync();
            return null;
        }
    }

    /// <summary>
    /// Cleanup expired temp sessions (run periodically)
    /// </summary>
    public async Task CleanupExpiredSessionsAsync(CancellationToken ct = default)
    {
        var now = _timeProvider.GetUtcNow();
        var cutoff = now.AddHours(-1); // Keep used sessions for 1 hour (audit trail)

        var expiredSessions = await _dbContext.TempSessions
            .Where(ts => ts.ExpiresAt < now.UtcDateTime || (ts.IsUsed && ts.UsedAt < cutoff.UtcDateTime))
            .ToListAsync(ct);

        if (expiredSessions.Any())
        {
            _dbContext.TempSessions.RemoveRange(expiredSessions);
            await _dbContext.SaveChangesAsync(ct);

            _logger.LogInformation("Cleaned up {Count} expired temp sessions", expiredSessions.Count);
        }
    }

    // Helper: Generate cryptographically secure 256-bit token
    private string GenerateSecureToken()
    {
        var bytes = new byte[TokenSizeBytes];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    // Helper: Hash token for storage (SHA-256)
    private static string HashToken(string token)
    {
        return CryptographyHelper.ComputeSha256HashBase64(token);
    }
}
