namespace Api.Models;

public record RegisterPayload(
    string Email,
    string Password,
    string? DisplayName,
    string? Role);

public class LoginPayload
{
    public string Email { get; set; } = default!;
    public string Password { get; set; } = default!;
}

public record RegisterCommand(
    string Email,
    string Password,
    string? DisplayName,
    string? Role,
    string? IpAddress,
    string? UserAgent);

public record LoginCommand(
    string Email,
    string Password,
    string? IpAddress,
    string? UserAgent);

public record AuthUser(
    string Id,
    string Email,
    string? DisplayName,
    string Role);

public record AuthResult(AuthUser User, string SessionToken, DateTime ExpiresAt);

public record ActiveSession(AuthUser User, DateTime ExpiresAt);

public record AuthResponse(AuthUser User, DateTime ExpiresAt);
