namespace Api.Models;

public record RegisterPayload(
    string email,
    string password,
    string? displayName,
    string? role);

public class LoginPayload
{
    public string email { get; set; } = default!;
    public string password { get; set; } = default!;
}

public record RegisterCommand(
    string email,
    string password,
    string? displayName,
    string? role,
    string? ipAddress,
    string? userAgent);

public record LoginCommand(
    string email,
    string password,
    string? ipAddress,
    string? userAgent);

public record AuthUser(
    string id,
    string email,
    string? displayName,
    string role);

public record AuthResult(AuthUser User, string SessionToken, DateTime ExpiresAt);

public record ActiveSession(AuthUser User, DateTime ExpiresAt);

public record AuthResponse(AuthUser user, DateTime expiresAt);
