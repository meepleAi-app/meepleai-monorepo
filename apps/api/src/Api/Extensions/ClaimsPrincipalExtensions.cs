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

    /// <summary>
    /// True when the principal has admin privileges (Admin or SuperAdmin).
    ///
    /// Issue #2845 / finding #HH: role claims are PascalCase
    /// (<see cref="Api.Infrastructure.Authentication"/> normalizes to
    /// "SuperAdmin" / "Admin" / "Editor"). Handlers that checked only
    /// <c>IsInRole("Admin")</c> denied a superadmin the admin path — e.g. the
    /// shared-game delete fell through to the Editor "request" branch (202,
    /// no-op) for a superadmin.
    /// </summary>
    public static bool IsAdmin(this ClaimsPrincipal user) =>
        user.IsInRole("Admin") || user.IsInRole("SuperAdmin");

    /// <summary>
    /// True when the principal is admin, superadmin, or editor. Mirrors the
    /// "AdminOrEditorPolicy" role set (SuperAdmin, Admin, Editor).
    /// </summary>
    public static bool IsAdminOrEditor(this ClaimsPrincipal user) =>
        user.IsInRole("Admin") || user.IsInRole("SuperAdmin") || user.IsInRole("Editor");
}
