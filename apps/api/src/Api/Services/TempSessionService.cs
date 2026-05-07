using System.Security.Cryptography;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

/// <summary>
/// Service for temporary 2FA sessions
/// AUTH-07: Manages short-lived, single-use tokens between password and 2FA verification
/// </summary>
internal class TempSessionService : ITempSessionService
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<TempSessionService> _logger;
    private readonly TimeProvider _timeProvider;

    private const int TempSessionLifetimeMinutes = 5;
    private const int TokenSizeBytes = 32; // 256 bits

    /// <summary>
    /// C6: maximum failed 2FA attempts allowed against a single temp session
    /// before it is invalidated. The user must re-authenticate to mint a new
    /// temp session — there is no permanent per-user lockout (DoS-aware).
    /// </summary>
    private const int MaxFailedAttempts = 5;

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
        await _dbContext.SaveChangesAsync().ConfigureAwait(false);

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
        using var transaction = await _dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable).ConfigureAwait(false);
        try
        {
            var tempSession = await _dbContext.TempSessions
                .FirstOrDefaultAsync(ts => ts.TokenHash == tokenHash && !ts.IsUsed && ts.ExpiresAt > now).ConfigureAwait(false);

            if (tempSession == null)
            {
                _logger.LogWarning("Temp session validation failed: token not found, expired, or already used");
                return null;
            }

            // Mark as used (single-use enforcement)
            tempSession.IsUsed = true;
            tempSession.UsedAt = _timeProvider.GetUtcNow().UtcDateTime;
            await _dbContext.SaveChangesAsync().ConfigureAwait(false);
            await transaction.CommitAsync().ConfigureAwait(false);

            _logger.LogInformation("Temp session validated and consumed for user {UserId}", tempSession.UserId);
            return tempSession.UserId;
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error during temp session validation");
            await transaction.RollbackAsync().ConfigureAwait(false);
            return null;
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(ex, "Cryptographic error during temp session token validation");
            await transaction.RollbackAsync().ConfigureAwait(false);
            return null;
        }
    }

    /// <summary>
    /// C6: read-only validity check. Returns the userId iff the temp session
    /// exists, hasn't expired, hasn't been consumed, and hasn't crossed the
    /// failure threshold. Does NOT mark the session used — the success path
    /// is responsible for calling <see cref="ConsumeTempSessionAsync"/>.
    /// </summary>
    public async Task<Guid?> ValidateTempSessionAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        string tokenHash;
        try
        {
            tokenHash = HashToken(token);
        }
        catch (FormatException)
        {
            // Malformed Base64 cookie — fail closed.
            return null;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var session = await _dbContext.TempSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                ts => ts.TokenHash == tokenHash && !ts.IsUsed && ts.ExpiresAt > now,
                cancellationToken)
            .ConfigureAwait(false);

        if (session == null)
        {
            return null;
        }

        // C6: even if IsUsed isn't yet set, an over-threshold session must be
        // rejected so a final "valid" code can't sneak through after the
        // session was supposed to be locked.
        if (session.FailedAttemptCount >= MaxFailedAttempts)
        {
            return null;
        }

        return session.UserId;
    }

    /// <summary>
    /// C6: marks temp session as used (single-use enforcement). Call only on
    /// the successful 2FA verify branch.
    /// </summary>
    public async Task ConsumeTempSessionAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        string tokenHash;
        try
        {
            tokenHash = HashToken(token);
        }
        catch (FormatException)
        {
            return;
        }

        var session = await _dbContext.TempSessions
            .FirstOrDefaultAsync(ts => ts.TokenHash == tokenHash && !ts.IsUsed, cancellationToken)
            .ConfigureAwait(false);

        if (session == null)
        {
            return;
        }

        session.IsUsed = true;
        session.UsedAt = _timeProvider.GetUtcNow().UtcDateTime;
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// C6: records a failed 2FA verification attempt against the temp session.
    /// Returns true iff this attempt either crossed the failure threshold
    /// (and the session is now invalidated) or the session was already gone /
    /// invalidated when the call landed. Below threshold returns false so the
    /// caller can surface "wrong code, try again" instead of "session lost".
    /// Post-invalidation calls are idempotent: counter and IsUsed do not
    /// mutate further.
    /// </summary>
    public async Task<bool> RecordFailedAttemptAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return true;
        }

        string tokenHash;
        try
        {
            tokenHash = HashToken(token);
        }
        catch (FormatException)
        {
            return true;
        }

        var session = await _dbContext.TempSessions
            .FirstOrDefaultAsync(ts => ts.TokenHash == tokenHash, cancellationToken)
            .ConfigureAwait(false);

        if (session == null)
        {
            // No row — treat as invalidated (caller's path must error out).
            return true;
        }

        // Idempotent on already-invalidated sessions: the row is locked
        // either by IsUsed or by FailedAttemptCount >= threshold. Don't
        // bump the counter further or reopen IsUsed.
        if (session.IsUsed || session.FailedAttemptCount >= MaxFailedAttempts)
        {
            return true;
        }

        session.FailedAttemptCount++;
        var nowInvalidated = session.FailedAttemptCount >= MaxFailedAttempts;
        if (nowInvalidated)
        {
            session.IsUsed = true;
            session.UsedAt = _timeProvider.GetUtcNow().UtcDateTime;
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Failed to record 2FA failed attempt");
            return false;
        }

        return nowInvalidated;
    }

    /// <summary>
    /// Cleanup expired temp sessions (run periodically)
    /// </summary>
    public async Task CleanupExpiredSessionsAsync(CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var cutoff = now.AddHours(-1); // Keep used sessions for 1 hour (audit trail)

        var expiredSessions = await _dbContext.TempSessions
            .Where(ts => ts.ExpiresAt < now.UtcDateTime || (ts.IsUsed && ts.UsedAt < cutoff.UtcDateTime))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        if (expiredSessions.Count > 0)
        {
            _dbContext.TempSessions.RemoveRange(expiredSessions);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Cleaned up {Count} expired temp sessions", expiredSessions.Count);
        }
    }

    // Helper: Generate cryptographically secure 256-bit token
    private static string GenerateSecureToken()
    {
        var bytes = new byte[TokenSizeBytes];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    // Helper: Hash token for storage (SHA-256 of decoded Base-64 bytes).
    // C1 fix: route through SessionTokenHasher so all session-token hashing in the codebase
    // shares one algorithm. Previously this used CryptographyHelper.ComputeSha256HashBase64
    // which hashes UTF-8 bytes of the Base-64 *string* — algorithmically inconsistent with
    // SessionToken.ComputeHash and causing endpoint lookups to fail (C1).
    private static string HashToken(string token)
    {
        return SessionTokenHasher.HashFromCookie(token);
    }
}
