using System.Security.Cryptography;
using Api.Helpers;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

public class AuthService
{
    public const string SessionCookieName = "meeple_session";
    private const string ElevatedRoleAssignmentError = "Only administrators can assign elevated roles.";
    private const int SessionTokenSize = 32;
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(7);
    private readonly MeepleAiDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly ISessionCacheService? _sessionCache;
    private readonly IPasswordHashingService _passwordHashingService;

    public AuthService(
        MeepleAiDbContext db,
        IPasswordHashingService passwordHashingService,
        ISessionCacheService? sessionCache = null,
        TimeProvider? timeProvider = null)
    {
        _db = db;
        _passwordHashingService = passwordHashingService;
        _sessionCache = sessionCache;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<AuthResult> RegisterAsync(RegisterCommand command, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.Email))
            throw new ArgumentException("Email is required", nameof(command.Email));

        if (!IsValidEmail(command.Email))
            throw new ArgumentException("Email format is invalid", nameof(command.Email));

        if (string.IsNullOrWhiteSpace(command.Password) || command.Password.Length < 8)
            throw new ArgumentException("Password must be at least 8 characters", nameof(command.Password));

        if (string.IsNullOrWhiteSpace(command.DisplayName))
            throw new ArgumentException("Display name is required", nameof(command.DisplayName));
        if (command.DisplayName.Trim().Length > 100)
            throw new ArgumentException("Display name must not exceed 100 characters", nameof(command.DisplayName));

        var email = command.Email.Trim().ToLowerInvariant();
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var existingUser = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (existingUser != null)
        {
            throw new InvalidOperationException("Email is already registered");
        }

        var hasAnyUsers = await _db.Users.AnyAsync(ct);
        var requestedRole = ParseRole(command.Role);

        if (hasAnyUsers && requestedRole != UserRole.User)
        {
            throw new InvalidOperationException(ElevatedRoleAssignmentError);
        }

        var role = hasAnyUsers ? UserRole.User : requestedRole;

        var user = new UserEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            Email = email,
            DisplayName = command.DisplayName?.Trim(),
            PasswordHash = HashPassword(command.Password),
            Role = role,
            CreatedAt = now
        };
        _db.Users.Add(user);

        var session = CreateSessionEntity(user, command.IpAddress, command.UserAgent, now);
        _db.UserSessions.Add(session.Entity);

        await _db.SaveChangesAsync(ct);

        return new AuthResult(ToDto(user), session.Token, session.Entity.ExpiresAt);
    }

    public async Task<AuthResult?> LoginAsync(LoginCommand command, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.Email) || string.IsNullOrWhiteSpace(command.Password))
        {
            return null;
        }

        var email = command.Email.Trim().ToLowerInvariant();
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user == null)
        {
            return null;
        }

        if (!VerifyPassword(command.Password, user.PasswordHash))
        {
            return null;
        }

        var session = CreateSessionEntity(user, command.IpAddress, command.UserAgent, now);
        _db.UserSessions.Add(session.Entity);
        await _db.SaveChangesAsync(ct);

        return new AuthResult(ToDto(user), session.Token, session.Entity.ExpiresAt);
    }

    /// <summary>
    /// Creates a new session for a user by their ID (used for password reset auto-login).
    /// </summary>
    public async Task<AuthResult?> CreateSessionForUserAsync(
        string userId,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user == null)
        {
            return null;
        }

        var session = CreateSessionEntity(user, ipAddress, userAgent, now);
        _db.UserSessions.Add(session.Entity);
        await _db.SaveChangesAsync(ct);

        return new AuthResult(ToDto(user), session.Token, session.Entity.ExpiresAt);
    }

    public async Task<ActiveSession?> ValidateSessionAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var hash = HashToken(token);

        // Try cache first (Phase 2 optimization)
        if (_sessionCache != null)
        {
            var cached = await _sessionCache.GetAsync(hash, ct);
            if (cached != null)
            {
                // Verify not expired (belt-and-suspenders check)
                if (cached.ExpiresAt > now)
                {
                    // TEST-656: Always refresh user data from DB to get current role
                    // Session cache stores user snapshot, but roles can change
                    var currentUser = await _db.Users
                        .AsNoTracking()
                        .FirstOrDefaultAsync(u => u.Id == cached.User.Id, ct);
                    
                    if (currentUser == null)
                    {
                        // User deleted - invalidate session
                        await _sessionCache.InvalidateAsync(hash, ct);
                        return null;
                    }

                    // Update last seen synchronously to avoid disposed context issues
                    var session = await _db.UserSessions
                        .FirstOrDefaultAsync(s => s.TokenHash == hash, ct);
                    if (session != null)
                    {
                        session.LastSeenAt = now;
                        await _db.SaveChangesAsync(ct);
                    }

                    // Return session with CURRENT user data (fresh role from DB)
                    return new ActiveSession(ToDto(currentUser), cached.ExpiresAt, now);
                }

                // Expired in cache - invalidate
                await _sessionCache.InvalidateAsync(hash, ct);
            }
        }

        // Cache miss or no cache - query database
        var dbSession = await _db.UserSessions
            .AsNoTracking() // PERF-05: Read-only query
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.TokenHash == hash, ct);

        if (!IsSessionValid(dbSession, now))
        {
            return null;
        }

        // Update last seen (need to query again without AsNoTracking to update)
        var sessionToUpdate = await _db.UserSessions
            .FirstOrDefaultAsync(s => s.TokenHash == hash, ct);
        if (sessionToUpdate != null)
        {
            sessionToUpdate.LastSeenAt = now;
            await _db.SaveChangesAsync(ct);
        }

        var activeSession = new ActiveSession(ToDto(dbSession.User), dbSession.ExpiresAt, now);

        // Cache for next time
        if (_sessionCache != null)
        {
            await _sessionCache.SetAsync(hash, activeSession, dbSession.ExpiresAt, ct);
        }

        return activeSession;
    }

    public async Task LogoutAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        var hash = HashToken(token);
        var session = await _db.UserSessions.FirstOrDefaultAsync(s => s.TokenHash == hash, ct);
        if (session == null)
        {
            return;
        }

        session.RevokedAt = _timeProvider.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(ct);

        // Invalidate cache
        if (_sessionCache != null)
        {
            await _sessionCache.InvalidateAsync(hash, ct);
        }
    }

    private static (UserSessionEntity Entity, string Token) CreateSessionEntity(UserEntity user, string? ipAddress, string? userAgent, DateTime now)
    {
        var tokenBytes = RandomNumberGenerator.GetBytes(SessionTokenSize);
        var token = Convert.ToBase64String(tokenBytes);
        var expires = now.Add(SessionLifetime);

        var entity = new UserSessionEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            UserId = user.Id,
            TokenHash = HashToken(token),
            CreatedAt = now,
            ExpiresAt = expires,
            LastSeenAt = now,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            User = user
        };

        return (entity, token);
    }

    private string HashPassword(string password)
    {
        return _passwordHashingService.HashSecret(password);
    }

    private bool VerifyPassword(string password, string encodedHash)
    {
        return _passwordHashingService.VerifySecret(password, encodedHash);
    }

    private static string HashToken(string token)
    {
        return CryptographyHelper.ComputeSha256HashBase64(token);
    }

    /// <summary>
    /// Validates whether a session is valid and active.
    /// Checks for null session, null user, revoked status, and expiration.
    /// </summary>
    private static bool IsSessionValid(UserSessionEntity? session, DateTime now)
    {
        return session != null
            && session.User != null
            && session.RevokedAt == null
            && session.ExpiresAt > now;
    }

    private static AuthUser ToDto(UserEntity user) => new(
        user.Id,
        user.Email,
        user.DisplayName,
        user.Role.ToString());

    private static UserRole ParseRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return UserRole.User;
        }

        return Enum.TryParse<UserRole>(role, true, out var parsed) ? parsed : UserRole.User;
    }

    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        var trimmedEmail = email.Trim();

        // Basic email validation: must contain @ and have domain part
        var atIndex = trimmedEmail.IndexOf('@');
        if (atIndex <= 0 || atIndex == trimmedEmail.Length - 1)
            return false;

        var lastAtIndex = trimmedEmail.LastIndexOf('@');
        if (atIndex != lastAtIndex)
            return false; // Multiple @ symbols

        var domainPart = trimmedEmail.Substring(atIndex + 1);
        if (!domainPart.Contains('.'))
            return false; // No dot in domain

        return true;
    }
}