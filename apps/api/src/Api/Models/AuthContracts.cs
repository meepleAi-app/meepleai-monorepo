namespace Api.Models;

public record RegisterPayload(
    string tenantId,
    string email,
    string password,
    string? displayName,
    string? role,
    string? tenantName);

public record LoginPayload(string tenantId, string email, string password);

public record RegisterCommand(
    string tenantId,
    string? tenantName,
    string email,
    string password,
    string? displayName,
    string? role,
    string? ipAddress,
    string? userAgent);

public record LoginCommand(
    string tenantId,
    string email,
    string password,
    string? ipAddress,
    string? userAgent);

public record AuthUser(
    string id,
    string tenantId,
    string email,
    string? displayName,
    string role);

public record AuthResult(AuthUser User, string SessionToken, DateTime ExpiresAt);

public record ActiveSession(AuthUser User, DateTime ExpiresAt);

public record AuthResponse(AuthUser user, DateTime expiresAt);
