using System.Security.Cryptography;
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

    private const int TempSessionLifetimeMinutes = 5;
    private const int TokenSizeBytes = 32; // 256 bits

    public TempSessionService(MeepleAiDbContext dbContext, ILogger<TempSessionService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Create temp session with cryptographically secure token
    /// </summary>
    public async Task<string> CreateTempSessionAsync(string userId, string? ipAddress = null)
    {
        // Generate secure random token
        var token = GenerateSecureToken();
        var tokenHash = HashToken(token);

        var tempSession = new TempSessionEntity
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            TokenHash = tokenHash,
            IpAddress = ipAddress,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(TempSessionLifetimeMinutes),
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
    public async Task<string?> ValidateAndConsumeTempSessionAsync(string token)
    {
        var tokenHash = HashToken(token);
        var now = DateTime.UtcNow;

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
            tempSession.UsedAt = DateTime.UtcNow;
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
        var now = DateTime.UtcNow;
        var cutoff = now.AddHours(-1); // Keep used sessions for 1 hour (audit trail)

        var expiredSessions = await _dbContext.TempSessions
            .Where(ts => ts.ExpiresAt < now || (ts.IsUsed && ts.UsedAt < cutoff))
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
    private string HashToken(string token)
    {
        using var sha256 = SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(token);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}
