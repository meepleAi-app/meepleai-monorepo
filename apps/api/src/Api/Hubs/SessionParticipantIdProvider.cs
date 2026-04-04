using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace Api.Hubs;

/// <summary>
/// Custom IUserIdProvider for SignalR that supports both JWT users and guest participants.
/// JWT users: returns the standard user ID from claims.
/// Guests: returns "guest:{token}" from X-Session-Token header/query.
/// E3-4: Guest SignalR Auth.
/// </summary>
internal sealed class SessionParticipantIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        // 1. Try JWT claim first (standard auth)
        var httpContext = connection.GetHttpContext();
        var sessionToken = httpContext?.Request.Query["sessionToken"].FirstOrDefault()
            ?? httpContext?.Request.Headers["X-Session-Token"].FirstOrDefault();

        return ResolveUserId(connection.User, sessionToken);
    }

    /// <summary>
    /// Pure logic for resolving user identity. Extracted for testability.
    /// JWT claim takes priority, then session token, then null.
    /// </summary>
    internal static string? ResolveUserId(ClaimsPrincipal? user, string? sessionToken)
    {
        // 1. Try JWT claim first (standard auth)
        var userIdClaim = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user?.FindFirst("sub")?.Value;

        if (!string.IsNullOrEmpty(userIdClaim))
            return userIdClaim;

        // 2. Try session token (guest auth)
        if (!string.IsNullOrEmpty(sessionToken))
            return $"guest:{sessionToken}";

        return null;
    }
}
