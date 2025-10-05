using System.Security.Cryptography;
using System.Text;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Services;

public class AuthService
{
    public const string SessionCookieName = "meeple_session";
    private const int SessionTokenSize = 32;
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(7);
    private readonly MeepleAiDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly string _tenantId;
    private readonly string _tenantName;

    public AuthService(MeepleAiDbContext db, IOptions<SingleTenantOptions> tenantOptions, TimeProvider? timeProvider = null)
    {
        _db = db;
        _timeProvider = timeProvider ?? TimeProvider.System;
        var options = tenantOptions?.Value ?? new SingleTenantOptions();
        _tenantId = options.GetTenantId();
        _tenantName = options.GetTenantName();
    }

    public async Task<AuthResult> RegisterAsync(RegisterCommand command, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.email))
            throw new ArgumentException("Email is required", nameof(command.email));
        if (string.IsNullOrWhiteSpace(command.password) || command.password.Length < 8)
            throw new ArgumentException("Password must be at least 8 characters", nameof(command.password));

        var email = command.email.Trim().ToLowerInvariant();
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == _tenantId, ct);
        if (tenant == null)
        {
            tenant = new TenantEntity
            {
                Id = _tenantId,
                Name = _tenantName,
                CreatedAt = now
            };
            _db.Tenants.Add(tenant);
        }

        var existingUser = await _db.Users.FirstOrDefaultAsync(u => u.TenantId == _tenantId && u.Email == email, ct);
        if (existingUser != null)
        {
            throw new InvalidOperationException("Email is already registered");
        }

        var role = ParseRole(command.role);

        var user = new UserEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            TenantId = _tenantId,
            Email = email,
            DisplayName = command.displayName?.Trim(),
            PasswordHash = HashPassword(command.password),
            Role = role,
            CreatedAt = now,
            Tenant = tenant
        };
        _db.Users.Add(user);

        var session = CreateSessionEntity(user, command.ipAddress, command.userAgent, now);
        _db.UserSessions.Add(session.Entity);

        await _db.SaveChangesAsync(ct);

        return new AuthResult(ToDto(user), session.Token, session.Entity.ExpiresAt);
    }

    public async Task<AuthResult?> LoginAsync(LoginCommand command, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.email) || string.IsNullOrWhiteSpace(command.password))
        {
            return null;
        }

        var email = command.email.Trim().ToLowerInvariant();
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var user = await _db.Users.FirstOrDefaultAsync(u => u.TenantId == _tenantId && u.Email == email, ct);
        if (user == null)
        {
            return null;
        }

        if (!VerifyPassword(command.password, user.PasswordHash))
        {
            return null;
        }

        var session = CreateSessionEntity(user, command.ipAddress, command.userAgent, now);
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
            TenantId = user.TenantId,
            UserId = user.Id,
            TokenHash = HashToken(token),
            CreatedAt = now,
            ExpiresAt = expires,
            LastSeenAt = now,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Tenant = user.Tenant,
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
