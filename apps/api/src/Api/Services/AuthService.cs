using System.Security.Cryptography;
using System.Text;
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
    public AuthService(MeepleAiDbContext db, TimeProvider? timeProvider = null)
    {
        _db = db;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<AuthResult> RegisterAsync(RegisterCommand command, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.Email))
            throw new ArgumentException("Email is required", nameof(command.Email));
        if (string.IsNullOrWhiteSpace(command.Password) || command.Password.Length < 8)
            throw new ArgumentException("Password must be at least 8 characters", nameof(command.Password));

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

    public async Task<ActiveSession?> ValidateSessionAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var hash = HashToken(token);

        var session = await _db.UserSessions
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.TokenHash == hash, ct);

        if (session == null || session.RevokedAt != null || session.ExpiresAt <= now)
        {
            return null;
        }

        session.LastSeenAt = now;
        await _db.SaveChangesAsync(ct);

        return new ActiveSession(ToDto(session.User), session.ExpiresAt);
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

    private static string HashPassword(string password)
    {
        const int iterations = 210_000;
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, 32);
        return $"v1.{iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    private static bool VerifyPassword(string password, string encodedHash)
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

    private static string HashToken(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
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
}
