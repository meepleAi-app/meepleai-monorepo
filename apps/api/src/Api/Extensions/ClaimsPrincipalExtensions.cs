using System.Security.Claims;

namespace Api.Extensions;

/// <summary>
/// Extension methods for ClaimsPrincipal.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Gets the user ID from the claims principal.
    /// Looks for NameIdentifier or "sub" claim.
    /// </summary>
    /// <param name="user">The claims principal.</param>
    /// <returns>The user ID, or Guid.Empty if not found.</returns>
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value;

        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }
}
